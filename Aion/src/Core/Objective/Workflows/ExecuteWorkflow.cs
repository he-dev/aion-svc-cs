using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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
    public async Task<StepResultCollection> Now(Workflow workflow)
    {
        if (workflow is { Mode: WorkflowMode.Cron })
        {
            return [];
        }

        using var activity = new Activity(nameof(ExecuteWorkflow)).Start();
        using var registration = mapLogEvent.To(workflow.Logger);
        using var scope = logger.BeginScopeWith
        (
            ("aion.workflow.path", workflow.Path.Workflow),
            ("aion.workflow.mode", workflow.Mode),
            ("aion.workflow.execution_id", registration.ExecutionId)
        );

        logger.LogInformation("Executing workflow '{aion.workflow.path}'", workflow.Path.Workflow);

        // core: Does not filter out disabled steps because we want them logged.
        var stepResults = new StepResultCollection();
        try
        {
            foreach (var step in workflow.Steps)
            {
                var stepResult = await executeStep.Now(step);
                stepResults.Add(stepResult);
            }

            activity.SetStatus(ActivityStatusCode.Ok).Stop();

            logger.LogInformation
            (
                "Workflow '{aion.workflow.path}' completed in {aion.workflow.duration_ms:N0} ms. Steps: {aion.workflow.steps.total}, Okay: {aion.workflow.steps.passed}, Fail: {aion.workflow.steps.failed}, Timeout: {aion.workflow.steps.timeout}.",
                workflow.Path.Workflow,
                activity.Duration,
                stepResults.Count,
                stepResults.Okay,
                stepResults.Fail,
                stepResults.Timeout
            );

            logger.LogDebug("OkayRate: '{aion.workflow.okay_rate}', FailRate: {aion.workflow.fail_rate}.", stepResults.OkayRate, stepResults.FailRate);

            return stepResults;
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Workflow '{aion.workflow.path}' execution failed in {aion.workflow.duration_ms:N0}.", workflow.Path.Workflow, activity.Duration);
            throw;
        }
    }
}

public class StepResultCollection : Collection<StepResult>
{
    public int Okay => this.Count(result => result.Status == StepStatus.Okay);
    public int Fail => this.Count(result => result.Status is StepStatus.Fail);
    public int Timeout => this.Count(result => result.Status == StepStatus.Timeout);
    public int OkayRate => Count == 0 ? 0 : (Okay * 100) / Count;
    public int FailRate => Count == 0 ? 0 : (Fail * 100) / Count;
}