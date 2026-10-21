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
    public async Task<int?> Invoke
    (
        string fileName,
        string arguments,
        string workingDirectory,
        IDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
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

        var stdOutCompletion = new TaskCompletionSource<bool>();
        var stdErrCompletion = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) => { OnDataReceived(e, stdOutCompletion, ConsoleStreamType.StdOut); };
        process.ErrorDataReceived += (_, e) => { OnDataReceived(e, stdErrCompletion, ConsoleStreamType.StdErr); };

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (process.Start())
            {
                try
                {
                    logger.User.LogInformation
                    (
                        "Process {aion.workflow.process.id} started by '{aion.workflow.process.file_name}' with arguments '{aion.workflow.process.arguments}'.",
                        process.Id,
                        process.StartInfo.FileName,
                        process.StartInfo.Arguments
                    );

                    // util: This is a convenience ready-to-use command for killing the process.
                    logger.System.LogDebug
                    (
                        "taskkill /F /FI \"PID eq {aion.workflow.process.id}\" /FI \"SESSION eq {aion.workflow.process.sid}\" /FI \"IMAGENAME eq {aion.workflow.process.image_name}\" /FI \"SERVICES eq false\"",
                        process.Id,
                        process.SessionId,
                        process.ProcessName
                    );
                }
                catch (InvalidOperationException)
                {
                    // meta: The process exited before its diagnostic details could be read.
                }

                // core: Background processes are not allowed to expect any input. Not respecting the EOF means for them, they're gonna hit the timeout.
                process.StandardInput.Close();

                // meta: Reads the output stream first and then waits because deadlocks are possible.
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(linkedCts.Token);
                await Task.WhenAll(stdOutCompletion.Task, stdErrCompletion.Task).WaitAsync(linkedCts.Token);

                return process.ExitCode;
            }
        }
        catch (OperationCanceledException)
        {
            if (timeoutCts.IsCancellationRequested)
            {
                logger.User.LogWarning("Process {aion.workflow.process.id} timed out after {aion.workflow.process.timeout_s} s.", process.Id, timeout.TotalSeconds);
            }

            var graceTimeout = TimeSpan.FromSeconds(10);
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    // core: Let's give the process a chance to exit.
                    logger.System.LogDebug("Waiting {aion.workflow.process.kill_timeout_s} s for process {aion.workflow.process.id} to exit.", graceTimeout.TotalSeconds, process.Id);
                    using var cleanupCts = new CancellationTokenSource(graceTimeout);
                    await process.WaitForExitAsync(cleanupCts.Token);
                    logger.System.LogInformation("Process {aion.workflow.process.id} was killed.", process.Id);
                }
            }
            catch (OperationCanceledException)
            {
                logger.System.LogWarning("Process {aion.workflow.process.id} termination could not be confirmed within {aion.workflow.process.kill_timeout_s} s after the kill request.", process.Id, graceTimeout.TotalSeconds);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                logger.System.LogInformation("Process {aion.workflow.process.id} had already exited.", process.Id);
            }
            catch (Exception ex)
            {
                logger.System.LogError(ex, "Unable to kill process {aion.workflow.process.id}.", process.Id);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        return null;
    }

    // util: Let's not write this code twice...
    private void OnDataReceived(DataReceivedEventArgs e, TaskCompletionSource<bool> stdStreamCompletion, ConsoleStreamType consoleStreamType)
    {
        // meta: The output stream has been closed, i.e., the process has terminated.
        if (e.Data is null)
        {
            stdStreamCompletion.TrySetResult(true);
            logger.System.LogDebug("End of console stream.");
        }
        else
        {
            switch (consoleStreamType)
            {
                case ConsoleStreamType.StdOut: logger.User.LogInformation("{StdOut}: {aion.workflow.process.stdout}", consoleStreamType, e.Data); break;
                case ConsoleStreamType.StdErr: logger.User.LogError("{StdErr}: {aion.workflow.process.stderr}", consoleStreamType, e.Data); break;
            }
        }
    }
}

// core: Allows us to distinguish the source of various log entries.
public enum ConsoleStreamType
{
    StdOut,
    StdErr,
}