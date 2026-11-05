using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Meta.Serilog;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;
using Wiretap.Core;

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
        using var loggerBug = forkLogging.Into(new LoggerFork(workflow.Logger));
        using var about = logger.BeginScopeAbout<ExecuteWorkflow>
        ([
            ("@workflow.name", workflow.Spec.Path),
            ("workflow.mode", workflow.TriggerMode)
        ]);

        about.Zero.With(Story.System).LogDebug(null);

        var stepResults = new List<bool>();
        try
        {
            foreach (var step in workflow.Steps)
            {
                if (step.Enabled)
                {
                    stepResults.Add(await executeStep.AsProcess(step, cancellationToken));
                }
                else
                {
                    about.Tick.With(Story.System).LogInformation("Skipping step {workflow.step.index} because it is disabled.", step.Index);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var stats = logger.BeginScopeWith
            (
                ("workflow.step.count.enabled", workflow.Steps.Count(step => step.Enabled)),
                ("workflow.step.count.success", stepResults.Count(result => result)),
                ("workflow.step.count.failure", stepResults.Count(result => !result))
            );
            about
                .Last
                .With(Story.System)
                .LogInformation("Workflow '{@workflow.name}' completes in {scene.duration_ms:N0} ms. Steps: {workflow.step.count.success} okay, {workflow.step.count_failure} failed.");
        }
        catch (OperationCanceledException)
        {
            about.Last.With(Story.System).LogError("Workflow '{@workflow.name}' canceled after {scene.duration_ms:N0} ms.");
        }

        catch (Exception ex)
        {
            about.Last.With(Story.System).LogError(ex, "Workflow '{@workflow.name}' fails after {scene.duration_ms:N0} ms.");
        }
    }

    public Task<bool> Step(Workflow.Step step, CancellationToken cancellationToken)
    {
        return executeStep.AsProcess(step, cancellationToken);
    }
}