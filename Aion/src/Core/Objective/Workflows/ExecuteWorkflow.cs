using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Meta.Logging.Wiretap;
using Aion.Meta.Serilog;
using Aion.Util.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Objective.Workflows;

public class ExecuteWorkflow
(
    ILogger<ExecuteWorkflow> logger,
    [FromKeyedServices("WorkflowLog")] MapLogEventByProperty mapLogEvent,
    ExecuteStep executeStep
)
{
    public async Task Now
    (
        Workflow workflow,
        ExecuteWorkflowProgress progress,
        CancellationToken cancellationToken = default
    )
    {
        using var registration = mapLogEvent.To(workflow.Logger);
        using var activity = logger.BeginActivity<ExecuteWorkflow>
        (
            ("aion.workflow.path", workflow.Spec.Path.Workflow),
            ("aion.workflow.mode", workflow.Mode),
            (mapLogEvent.PropertyName, registration.ExecutionId)
        );

        activity.Core.Zero.LogInformation("Executing workflow '{aion.workflow.path}'.");

        try
        {
            foreach (var step in workflow.Steps)
            {
                if (step.Enabled)
                {
                    await executeStep.Now(step, cancellationToken);
                }
                else
                {
                    activity.Core.LogInformation("Skipping step {aion.workflow.step.index} because it is disabled.", step.Index);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var stats = logger.BeginScopeWith
            (
                ("aion.workflow.steps.count", workflow.Steps.Count),
                ("aion.workflow.steps.passed", progress.Passed),
                ("aion.workflow.steps.failed", progress.Failed)
            );
            activity.Core.Okay.LogInformation("Workflow '{aion.workflow.path}' completed in {aion.activity.duration_ms:N0} ms. Steps: {aion.workflow.steps.passed} passed, {aion.workflow.steps.failed} failed.");
        }
        catch (OperationCanceledException)
        {
            activity.Core.Last.LogError("Workflow '{aion.workflow.path}' was canceled in {aion.activity.duration_ms:N0} ms.");
        }

        catch (Exception ex)
        {
            activity.Core.Last.LogError(ex, "Workflow '{aion.workflow.path}' failed in {aion.activity.duration_ms:N0} ms.");
        }
    }
}

public class ExecuteWorkflowProgress : Collection<ExecuteStepResult>, IProgress<ExecuteStepResult>
{
    public int Passed => this.Count(result => result.Process?.ExitCode == 0);
    public int Failed => this.Count(result => result.Process?.ExitCode != 0);

    public void Report(ExecuteStepResult value) => Add(value);
}