using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aion.Data;
using Aion.Services;
using Quartz;
using System.Management;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Reusable.Exceptionize;

namespace Aion.Jobs;

[UsedImplicitly]
[DisallowConcurrentExecution]
internal class WorkflowLauncher : IJob
{
    private readonly ILogger<WorkflowLauncher> _logger;
    private readonly WorkflowReader _workflowReader;

    public WorkflowLauncher(ILogger<WorkflowLauncher> logger, WorkflowReader workflowReader)
    {
        _logger = logger;
        _workflowReader = workflowReader;
    }

    // Executes this job.
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Executing job '{Name}'.", context.JobDetail.Key.Name);

        var workflowName = Path.Combine(context.JobDetail.JobDataMap.GetString(nameof(WorkflowService.Options.WorkflowsDirectory))!, $"{context.JobDetail.Key.Name}.json");
        var workflow = _workflowReader.ReadWorkflow(workflowName);
        await Execute(workflow, context.CancellationToken);
    }

    // Executes job's workflow.
    private async Task Execute(Workflow workflow, CancellationToken cancellationToken)
    {
        foreach (var step in workflow.Steps.Where(s => s.Enabled))
        {
            //await Execute(step, cancellationToken);
            var result = await ProcessHelper.StartAsync
            (
                step.FileName,
                step.Arguments ?? string.Empty,
                step.WorkingDirectory ?? string.Empty,
                step.TimeoutInMilliseconds
            );

            if (result.Success)
            {
                _logger.LogInformation("Step '{Name}' successfully finished:{NewLine} {Output}", step.Name, Environment.NewLine, result.Output);
            }
            else
            {
                _logger.LogError("Step '{Name}' failed with error code {ExitCode}:{NewLine} {Error}", step.Name, result.ExitCode, Environment.NewLine, result.Error);
                _logger.LogError("Exception:{NewLine} {Exception}", Environment.NewLine, result.Exception);

                if (step.OnError == OnError.Break)
                {
                    break;
                }
            }
        }
    }

    // Executes workflow's steps.
    // private async Task Execute(Step step, CancellationToken cancellationToken)
    // {
    //     var fileName = step.FileName;
    //     //var fileName = WorkflowVersion.FindLatest(step.FileName);
    //
    //     if (!File.Exists(step.FileName))
    //     {
    //         throw DynamicException.Create("FileNotFound", $"Step file '{step.FileName}' not found.");
    //     }
    //
    //     // if (step.SingleInstance && WindowsManagement.IsRunning(step.FileName, step.Arguments))
    //     // {
    //     //     _logger.LogInformation("Step '{FileName} {Arguments}' is already running.", step.FileName, step.Arguments);
    //     //     return;
    //     // }
    //
    //     using var process = new Process
    //     {
    //         StartInfo = new ProcessStartInfo
    //         {
    //             FileName = step.FileName,
    //             Arguments = step.Arguments,
    //             WindowStyle = step.WindowStyle,
    //             CreateNoWindow = true,
    //             UseShellExecute = false,
    //             RedirectStandardOutput = true,
    //             RedirectStandardError = true
    //             //WorkingDirectory = step.WorkingDirectory
    //         }
    //     };
    //
    //     if (process.Start())
    //     {
    //         _logger.LogInformation("Started step '{FileName} {Arguments}'.", step.FileName, step.Arguments);
    //
    //         await process.WaitForExitAsync(cancellationToken);
    //
    //         _logger.LogInformation(await process.StandardOutput.ReadToEndAsync());
    //         _logger.LogError(await process.StandardError.ReadToEndAsync());
    //
    //         if (process.ExitCode == 0)
    //         {
    //             _logger.LogInformation("Step '{FileName} {Arguments}' successfully finished.", step.FileName, step.Arguments);
    //         }
    //         else
    //         {
    //             _logger.LogError("Step '{FileName} {Arguments}' exited with error code: {ExitCode}", step.FileName, step.Arguments, process.ExitCode);
    //         }
    //     }
    //     else
    //     {
    //         _logger.LogInformation("Could not start '{FileName} {Arguments}'.", step.FileName, step.Arguments);
    //     }
    // }
}