using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Aion.Meta.Logging.Wiretap;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Buzz;

public class StartProcess(ILogger<StartProcess> logger)
{
    public async Task<StartProcessResult> Now
    (
        string fileName,
        string arguments,
        string workingDirectory,
        IDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new StartProcessResult(fileName) { Cancelled = true };
        }

        // core: Set the timeout for the process.
        // meta: This might throw on timeout out of range, so let's initialize it first.
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

        // note: If you run a bash-script on Linux, it is possible that ExitCode can be 255.
        // To fix it, you can try to add the "#!/bin/bash" header to the script.
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var (name, value) in environment)
        {
            process.StartInfo.Environment[name] = value;
        }

        // util: Let's measure the execution time.
        var stopwatch = Stopwatch.StartNew();

        var stdOutCompletion = new TaskCompletionSource<bool>();
        var stdErrCompletion = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) => { OnDataReceived(e, stdOutCompletion, ConsoleStreamType.StdOut); };
        process.ErrorDataReceived += (_, e) => { OnDataReceived(e, stdErrCompletion, ConsoleStreamType.StdErr); };

        var commandLine = $"{process.StartInfo.FileName} {process.StartInfo.Arguments}".Trim();

        if (cancellationToken.IsCancellationRequested)
        {
            return new StartProcessResult(fileName) { Cancelled = true };
        }

        try
        {
            logger.Buzz.Okay.LogInformation("Executing '{aion.workflow.step.command_line}'.", commandLine);

            if (process.Start())
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    try
                    {
                        // util: This is a convenience ready-to-use command for killing the process.
                        logger.LogDebug
                        (
                            "taskkill /F /FI \"PID eq {PID}\" /FI \"SESSION eq {SID}\" /FI \"IMAGENAME eq {ImageName}\" /FI \"SERVICES eq false\"",
                            process.Id,
                            process.SessionId,
                            process.ProcessName
                        );
                    }
                    catch (InvalidOperationException)
                    {
                        // meta: The process exited before its diagnostic details could be read.
                    }
                }

                // core: Background processes are not allowed to expect any input. Not respecting the EOF means for them, they're gonna hit the timeout.
                process.StandardInput.Close();

                // meta: Reads the output stream first and then waits because deadlocks are possible.
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(linkedCts.Token);
                await Task.WhenAll(stdOutCompletion.Task, stdErrCompletion.Task).WaitAsync(linkedCts.Token);

                switch (process.ExitCode)
                {
                    case 0: logger.Buzz.Last.LogInformation("Process '{aion.workflow.step.command_line}' completed in {aion.workflow.step.duration_ms:N0} ms.", commandLine, stopwatch.Elapsed.TotalMilliseconds); break;
                    default: logger.Buzz.Last.LogInformation("Process '{aion.workflow.step.command_line}' failed after {aion.workflow.step.duration_ms:N0} ms with exit-code {aion.workflow.step.exit_code}.", commandLine, stopwatch.Elapsed.TotalMilliseconds, process.ExitCode); break;
                }

                return new StartProcessResult(commandLine)
                {
                    ExitCode = process.ExitCode,
                    Duration = stopwatch.Elapsed
                };
            }

            // core: Zero duration means the process was not started.
            return new StartProcessResult(commandLine);
        }
        catch (OperationCanceledException)
        {
            var result = new StartProcessResult(commandLine)
            {
                ExitCode = process.HasExited ? process.ExitCode : null,
                Cancelled = cancellationToken.IsCancellationRequested,
                TimedOut = timeoutCts.IsCancellationRequested,
                Duration = stopwatch.Elapsed
            };

            if (cancellationToken.IsCancellationRequested)
            {
                logger.Buzz.LogInformation("Process '{aion.workflow.step.command_line}' was cancelled after {aion.workflow.step.duration_ms:N0} ms.", commandLine, stopwatch.Elapsed.TotalMilliseconds);
            }

            if (timeoutCts.IsCancellationRequested)
            {
                logger.Buzz.LogWarning("Process '{aion.workflow.step.command_line}' timed out after {aion.workflow.step.duration_ms:N0} ms.", commandLine, stopwatch.Elapsed.TotalMilliseconds);
            }

            try
            {
                if (!process.HasExited)
                {
                    var graceTimeout = TimeSpan.FromSeconds(10);
                    process.Kill(entireProcessTree: true);
                    // core: Let's give the process a chance to exit.
                    logger.Buzz.LogDebug("Waiting {0} seconds for process '{1}' to exit.", graceTimeout.TotalSeconds, commandLine);
                    using var cleanupCts = new CancellationTokenSource(graceTimeout);
                    await process.WaitForExitAsync(cleanupCts.Token);
                    logger.Buzz.LogWarning("Process '{aion.workflow.step.command_line}' was killed.", commandLine);
                }
            }
            catch (OperationCanceledException)
            {
                logger.Buzz.LogWarning("Termination of process '{aion.workflow.step.command_line}' could not be confirmed within 7 seconds after the kill request.", commandLine);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                logger.Buzz.LogInformation("Process '{aion.workflow.step.command_line}' had already exited.", commandLine);
            }
            catch (Exception ex)
            {
                logger.Buzz.LogError(ex, "Unable to kill '{aion.workflow.step.command_line}'.", commandLine);
                throw;
            }

            return result;
        }
    }

    // util: Let's not write this code twice...
    private void OnDataReceived(DataReceivedEventArgs e, TaskCompletionSource<bool> stdStreamCompletion, ConsoleStreamType consoleStreamType)
    {
        // meta: The output stream has been closed, i.e., the process has terminated.
        if (e.Data is null)
        {
            stdStreamCompletion.TrySetResult(true);
            logger.Buzz.LogTrace("End of console stream.");
        }
        else
        {
            switch (consoleStreamType)
            {
                case ConsoleStreamType.StdOut: logger.Buzz.LogInformation("{StdOut}: {aion.workflow.step.stdout}", e.Data, consoleStreamType); break;
                case ConsoleStreamType.StdErr: logger.Buzz.LogError("{StdErr}: {aion.workflow.step.stderr}", e.Data, consoleStreamType); break;
            }
        }
    }
}

public record StartProcessResult(string CommandLine)
{
    public int? ExitCode { get; init; }
    public bool Cancelled { get; init; }
    public bool? TimedOut { get; init; }
    public TimeSpan Duration { get; init; }
}

// core: Allows us to distinguish the source of various log entries.
public enum ConsoleStreamType
{
    StdOut,
    StdErr,
}