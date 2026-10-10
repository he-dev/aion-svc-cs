using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Buzz;

public class StartProcess(ILogger<StartProcess> logger)
{
    // note: Not using external cancellation as this app does not support such a scenario.
    public async Task<int> Now
    (
        string file,
        TimeSpan timeout,
        Action<ProcessStartInfo>? customizeProcessStartInfo = null,
        Action<string>? logStdErr = null,
        Action<string>? logStdOut = null
    )
    {
        // note: If you run a bash-script on Linux, it is possible that ExitCode can be 255.
        // To fix it, you can try to add the "#!/bin/bash" header to the script.
        var process = new Process
        {
            // meta: Set reasonable defaults and let the caller customize the rest.
            StartInfo = new ProcessStartInfo
            {
                FileName = file,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        customizeProcessStartInfo?.Invoke(process.StartInfo);

        // util: Let's measure the execution time.
        var stopwatch = Stopwatch.StartNew();

        var stdOutCompletion = new TaskCompletionSource<bool>();
        var stdErrCompletion = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) =>
        {
            OnDataReceived(e, stdOutCompletion, ConsoleStreamType.StdOut, stopwatch);
            if (e.Data is not null) logStdOut?.Invoke(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            OnDataReceived(e, stdErrCompletion, ConsoleStreamType.StdErr, stopwatch);
            if (e.Data is not null) logStdErr?.Invoke(e.Data);
        };

        try
        {
            logger.LogInformation("Executing file: '{File}'.", file);
            logger.LogInformation("With arguments: '{Args}'.", process.StartInfo.Arguments);

            if (process.Start())
            {
                // ReSharper disable once StringLiteralTypo - becasue it's nagging about the command.
                // util: This is a convenience ready-to-use command for killing the process.
                logger.LogDebug
                (
                    "taskkill /F /FI \"PID eq {PID}\" /FI \"SESSION eq {SID}\" /FI \"IMAGENAME eq {ImageName}\" /FI \"SERVICES eq false\"",
                    process.Id,
                    process.SessionId,
                    process.ProcessName
                );

                // core: Background processes are not allowed to expect any input. Not respecting the EOF means for them, they're gonna hit the timeout.
                process.StandardInput.Close();

                // meta: Reads the output stream first and then waits because deadlocks are possible.
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var cts = new CancellationTokenSource(timeout);
                await process.WaitForExitAsync(cts.Token);
                await Task.WhenAll(stdOutCompletion.Task, stdErrCompletion.Task);

                switch (process.ExitCode)
                {
                    case 0: logger.LogInformation("Process completed in {Elapsed:N0} ms.", stopwatch.Elapsed.TotalMilliseconds); break;
                    default: logger.LogError("Process failed after {Elapsed:N0} ms with code {ExitCode}.", stopwatch.Elapsed.TotalMilliseconds, process.ExitCode); break;
                }

                return process.ExitCode;
            }

            throw new ProcessNotStarted();
        }
        // core: This exception is thrown when the timeout is reached.
        catch (OperationCanceledException)
        {
            logger.LogWarning("Process timed out after {Elapsed:N0} ms.", stopwatch.Elapsed.TotalMilliseconds);
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    logger.LogWarning("Process was killed.");
                }
            }
            catch (InvalidOperationException)
            {
                logger.LogInformation("Process had already exited.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to kill process.");
            }

            throw new ProcessTimeout();
        }
        finally
        {
            process.Dispose();
        }
    }

    // util: Let's not write this code twice...
    private void OnDataReceived(DataReceivedEventArgs e, TaskCompletionSource<bool> stdStreamCompletion, ConsoleStreamType consoleStreamType, Stopwatch stopwatch)
    {
        // core: Allow the user to use this property in the message template.
        using var scope = logger.BeginScopeFrom(new { ConsoleStreamType = consoleStreamType });

        // meta: The output stream has been closed, i.e., the process has terminated.
        if (e.Data is null)
        {
            stdStreamCompletion.TrySetResult(true);

            // core: Allow the user to use this property in the message template.
            logger.LogTrace("EOF after {Duration} ms.", stopwatch.Elapsed.TotalMilliseconds);
        }
        else
        {
            switch (consoleStreamType)
            {
                case ConsoleStreamType.StdOut: logger.LogInformation("{Line}", e.Data); break;
                case ConsoleStreamType.StdErr: logger.LogError("{Line}", e.Data); break;
            }
        }
    }

    public void Kill() { }
}

// core: Allows us to distinguish the source of various log entries.
public enum ConsoleStreamType
{
    StdOut,
    StdErr,
}

public class ProcessTimeout : Exception;

public class ProcessNotStarted : Exception;