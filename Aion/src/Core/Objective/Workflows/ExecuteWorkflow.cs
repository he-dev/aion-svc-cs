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
    BugLogging bugLogging,
    ExecuteStep executeStep
)
{
    public async Task Invoke(Workflow workflow, CancellationToken cancellationToken = default)
    {
        using var loggerBug = bugLogging.With(new LoggerBug(workflow.Logger));
        using var activity = logger.BeginActivity<ExecuteWorkflow>
        (
            ("aion.workflow.path", workflow.Spec.Path.Workflow),
            ("aion.workflow.mode", workflow.TriggerMode)
        );

        activity.User.Zero.LogInformation("Executing workflow '{aion.workflow.path}'.");

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
                    activity.User.LogInformation("Skipping step {aion.workflow.step.index} because it is disabled.", step.Index);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var stats = logger.BeginScopeWith
            (
                ("aion.workflow.step.count.enabled", workflow.Steps.Count(step => step.Enabled)),
                ("aion.workflow.step.count.success", success.Count(result => result)),
                ("aion.workflow.step.count.failure", success.Count(result => !result))
            );
            activity.User.Okay.LogInformation("Workflow '{aion.workflow.path}' completed in {aion.activity.duration_ms:N0} ms. Steps: {aion.workflow.step.count.success} okay, {aion.workflow.step.count_failure} failed.");
        }
        catch (OperationCanceledException)
        {
            activity.User.Last.LogError("Workflow '{aion.workflow.path}' was canceled after {aion.activity.duration_ms:N0} ms.");
        }

        catch (Exception ex)
        {
            activity.User.Last.LogError(ex, "Workflow '{aion.workflow.path}' failed after {aion.activity.duration_ms:N0} ms.");
        }
    }
}