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
        if (workflow is { Mode: WorkflowMode.Cron, Enabled: false })
        {
            return [];
        }

        using var activity = new Activity(nameof(ExecuteWorkflow)).Start();
        using var scope = logger.BeginScopeFrom(new
        {
            WorkflowPath = workflow.Path,
            WorkflowMode = workflow.Mode,
        });

        using var registration = mapLogEvent.To(workflow.Logger);
        using var executionScope = logger.BeginScopeFrom(new { registration.ExecutionId });

        logger.LogInformation("Executing workflow: '{WorkflowPath}'", workflow.Path);

        // core: Does not filter out disabled steps because we want them logged.
        var stepResults = new StepResultCollection();
        try
        {
            foreach (var step in workflow.Steps)
            {
                var stepResult = await executeStep.Now(step);
                stepResults.Add(stepResult);

                if (stepResult.ExitCode is not null and not 0 && step.OnError is { } onError)
                {
                    if (onError.Trim().Equals("continue", StringComparison.OrdinalIgnoreCase))
                    {
                        // core: Just continue with the next step.
                    }

                    if (onError.Trim().Equals("break", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogError("Workflow execution stopped due to a failed step.");
                        break;
                    }
                }
            }

            activity.SetStatus(ActivityStatusCode.Ok).Stop();

            logger.LogInformation(
                "Workflow '{WorkflowPath}' completed in {Duration:N0} ms. Steps: {Total}, Passed: {Passed}, Failed: {Failed}.",
                workflow.Path, activity.Duration, stepResults.Count, stepResults.Passed, stepResults.Failed
            );

            return stepResults;
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Workflow '{WorkflowPath}' execution failed in {Duration:N0}.", workflow.Path, activity.Duration);
            throw;
        }
    }
}

public class StepResultCollection : Collection<StepResult>
{
    public int Actual => this.Count(result => result.ExitCode is not null);
    public int Passed => this.Count(result => result.ExitCode == 0);
    public int Failed => this.Count(result => result.ExitCode is not null && result.ExitCode != 0);
}