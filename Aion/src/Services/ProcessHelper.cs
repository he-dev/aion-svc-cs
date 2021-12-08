using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Aion.Services;

public static class ProcessHelper
{
    public static async Task<ProcessResult> StartAsync(string fileName, string arguments, string workingDirectory, int timeoutInMilliseconds)
    {
        // If you run bash-script on Linux it is possible that ExitCode can be 255.
        // To fix it you can try to add '#!/bin/bash' header to the script.
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            }
        };

        var outputBuilder = new StringBuilder();
        var outputCloseEvent = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) =>
        {
            // The output stream has been closed i.e. the process has terminated.
            if (e.Data is null)
            {
                outputCloseEvent.SetResult(true);
            }
            else
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        var errorBuilder = new StringBuilder();
        var errorCloseEvent = new TaskCompletionSource<bool>();

        process.ErrorDataReceived += (s, e) =>
        {
            // The error stream has been closed i.e. the process has terminated.
            if (e.Data is null)
            {
                errorCloseEvent.SetResult(true);
            }
            else
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        try
        {
            if (process.Start())
            {
                // Reads the output stream first and then waits because deadlocks are possible.
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Creates task to wait for process exit using timeout.
                var waitForExit = WaitForExitAsync(process, timeoutInMilliseconds);

                // Create task to wait for process exit and closing all output streams.
                var processTask = Task.WhenAll(waitForExit, outputCloseEvent.Task, errorCloseEvent.Task);

                // Waits process completion and then checks it was not completed by timeout.
                if (await Task.WhenAny(Task.Delay(timeoutInMilliseconds), processTask) == processTask && waitForExit.Result)
                {
                    return new ProcessResult(process.ExitCode)
                    {
                        Completed = true,
                        Output = outputBuilder.ToString(),
                        Error = errorBuilder.ToString()
                    };
                }

                // Kill it if it takes too long to complete or hangs.
                try
                {
                    process.Kill();
                    return new ProcessResult(-1)
                    {
                        TimedOut = true,
                        Killed = true,
                        Output = outputBuilder.ToString(),
                        Error = errorBuilder.ToString()
                    };
                }
                catch (Exception ex)
                {
                    return new ProcessResult(-1)
                    {
                        TimedOut = true,
                        Output = outputBuilder.ToString(),
                        Error = errorBuilder.ToString(),
                        Exception = ex
                    };
                }
            }
        }
        catch (Exception ex)
        {
            // Usually it occurs when an executable file is not found or is not executable.
            return new ProcessResult(-1)
            {
                Output = outputBuilder.ToString(),
                Error = errorBuilder.ToString(),
                Exception = ex,
            };
        }

        return new ProcessResult(0);
    }


    private static Task<bool> WaitForExitAsync(Process process, int timeout)
    {
        return Task.Run(() => process.WaitForExit(timeout));
    }


    [PublicAPI]
    public record ProcessResult(int ExitCode)
    {
        public bool Success => ExitCode == 0;
        public bool Completed { get; set; }
        public bool TimedOut { get; set; }
        public bool Killed { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public Exception? Exception { get; set; }
    }
}