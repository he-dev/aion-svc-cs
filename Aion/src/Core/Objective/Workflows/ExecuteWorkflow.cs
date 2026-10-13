using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Meta.Serilog;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Objective.Workflows;

public class ExecuteWorkflow
(
    ILogger<ExecuteWorkflow> logger,
    MapLogEvent mapLogEvent,
    ExecuteStep executeStep
)
{
    public async Task<bool> Now
    (
        Workflow workflow,
        ExecuteWorkflowProgress progress,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = new Activity(nameof(ExecuteWorkflow)).Start();
        using var registration = mapLogEvent.To(workflow.Logger);
        using var scope = logger.BeginScopeWith
        (
            ("aion.workflow.path", workflow.Path.Workflow),
            ("aion.workflow.mode", workflow.Mode),
            ("aion.workflow.execution_id", registration.ExecutionId)
        );

        logger.LogInformation("Executing workflow '{aion.workflow.path}'", workflow.Path.Workflow);

        try
        {
            foreach (var step in workflow.Steps)
            {
                progress.Report(await executeStep.Now(step, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
            }

            activity.SetStatus(ActivityStatusCode.Ok).Stop();

            logger.LogInformation
            (
                "Workflow '{aion.workflow.path}' completed in {aion.workflow.duration_ms:N0} ms. Steps: {aion.workflow.steps.total}, Okay: {aion.workflow.steps.passed}, Fail: {aion.workflow.steps.failed}, Timeout: {aion.workflow.steps.timeout}.",
                workflow.Path.Workflow,
                activity.Duration,
                progress.Count,
                progress.Okay,
                progress.Fail,
                progress.TimedOut
            );

            logger.LogDebug("OkayRate: '{aion.workflow.okay_rate}', FailRate: {aion.workflow.fail_rate}.", progress.OkayRate, progress.FailRate);

            return true;
        }
        catch(OperationCanceledException)
        {
            logger.LogError("Workflow '{aion.workflow.path}' execution was canceled in {aion.workflow.duration_ms:N0}.", workflow.Path.Workflow, activity.Duration);
            return false;
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Workflow '{aion.workflow.path}' execution failed in {aion.workflow.duration_ms:N0}.", workflow.Path.Workflow, activity.Duration);
            return false;
        }
    }
}

public class ExecuteWorkflowProgress : Collection<ExecuteStepResult>, IProgress<ExecuteStepResult>
{
    public int Okay => this.Count(result => result.Process?.ExitCode == 0);
    public int Fail => this.Count(result => result.Process?.ExitCode != 0);
    public int TimedOut => this.Count(result => result.Process?.TimedOut == true);
    public int OkayRate => Count == 0 ? 0 : (Okay * 100) / Count;
    public int FailRate => Count == 0 ? 0 : (Fail * 100) / Count;

    public void Report(ExecuteStepResult value) => Add(value);
}