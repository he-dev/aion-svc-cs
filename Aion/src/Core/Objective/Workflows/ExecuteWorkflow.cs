using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Meta.Logging.Wiretap;
using Aion.Meta.Serilog;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Objective.Workflows;

public class ExecuteWorkflow
(
    ILogger<ExecuteWorkflow> logger,
    ForkLogging forkLogging,
    ExecuteStep executeStep
)
{
    public async Task Invoke(Workflow workflow, CancellationToken cancellationToken = default)
    {
        using var loggerBug = forkLogging.With(new LoggerFork(workflow.Logger));
        using var activity = logger.BeginScopeAbout<ExecuteWorkflow>
        ([
            ("@workflow.name", workflow.Spec.Path),
            ("workflow.mode", workflow.TriggerMode)
        ]);

        activity.Domain.Zero.LogInformation("Executing workflow '{@workflow.name}'.");

        var success = new List<bool>();
        try
        {
            foreach (var step in workflow.Steps)
            {
                if (step.Enabled)
                {
                    success.Add(await executeStep.AsProcess(step, cancellationToken));
                }
                else
                {
                    activity.Domain.LogInformation("Skipping step {workflow.step.index} because it is disabled.", step.Index);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var stats = logger.BeginScopeWith
            (
                ("workflow.step.count.enabled", workflow.Steps.Count(step => step.Enabled)),
                ("workflow.step.count.success", success.Count(result => result)),
                ("workflow.step.count.failure", success.Count(result => !result))
            );
            activity.Domain.Tick.LogInformation("Workflow '{@workflow.name}' completed in {scene.duration_ms:N0} ms. Steps: {workflow.step.count.success} okay, {workflow.step.count_failure} failed.");
        }
        catch (OperationCanceledException)
        {
            activity.Domain.Last.LogError("Workflow '{@workflow.name}' was canceled after {scene.duration_ms:N0} ms.");
        }

        catch (Exception ex)
        {
            activity.Domain.Last.LogError(ex, "Workflow '{@workflow.name}' failed after {scene.duration_ms:N0} ms.");
        }
    }
}