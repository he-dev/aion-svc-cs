using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Wiretap.Core;

namespace Aion.Core.Magic.Workflows;

public class ExecuteWorkflow
(
    ILogger<ExecuteWorkflow> logger,
    ForkLogging forkLogging,
    ExecuteStep executeStep
)
{
    public async Task Invoke(Workflow workflow, WorkflowTriggerSource source, IImmutableList<string> steps, CancellationToken cancellationToken = default)
    {
        using var loggerBug = forkLogging.Into(new LoggerFork(workflow.Logger));
        using var about = logger.Baseline.BeginScopeAbout<ExecuteWorkflow>
        ([
            ("@workflow.name", workflow.Spec.Path),
            ("workflow.trigger.source", source)
        ]);

        about.Zero.LogDebug(null);

        try
        {
            if (steps.Count > 0)
            {
                about.Tick.LogInformation("Executing custom steps: {workflow.steps}.", steps);

                foreach (var name in steps)
                {
                    if (workflow.Steps.SingleOrDefault(s => name.Equals(s.Name, StringComparison.InvariantCultureIgnoreCase)) is { } step)
                    {
                        await executeStep.AsProcess(step, source, cancellationToken);
                    }
                    else
                    {
                        about.Tick.LogInformation("Skipping step {workflow.step.name} because it does not exist.", name);
                    }
                }
            }
            else
            {
                about.Tick.LogInformation("Executing default steps.");

                foreach (var step in workflow.Steps)
                {
                    if (step.Enabled)
                    {
                        await executeStep.AsProcess(step, source, cancellationToken);
                    }
                    else
                    {
                        about.Tick.LogInformation("Skipping step {workflow.step.index} because it is disabled.", step.Index);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            about.Last.LogInformation("Workflow '{@workflow.name}' completes in {activity.duration_ms:N0} ms.");
        }
        catch (OperationCanceledException)
        {
            about.Last.LogError("Workflow '{@workflow.name}' canceled after {activity.duration_ms:N0} ms.");
        }

        catch (Exception ex)
        {
            about.Last.LogError(ex, "Workflow '{@workflow.name}' fails after {activity.duration_ms:N0} ms.");
        }
    }
}