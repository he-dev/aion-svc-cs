using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Data.Scheduler;
using Aion.Data.Specs;
using Aion.Util;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiretap.Core;

namespace Aion.Core.Magic.Workflows;

public class ExecuteWorkflow
(
    ILogger<ExecuteWorkflow> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    ForkLogging forkLogging,
    ExecuteStep executeStep
)
{
    public async Task Invoke(WorkflowSpec workflow, WorkflowMode mode, IImmutableList<string> steps, CancellationToken cancellationToken = default)
    {
        using var loggerBug = forkLogging.Into(new LoggerFork(workflow.Logger));
        using var about = logger.Baseline.BeginScopeAbout<ExecuteWorkflow>
        ([
            ("@workflow.name", workflow.Path),
            ("workflow.mode", mode)
        ]);

        about.Zero.LogDebug(null);

        var variables =
            ImmutableList<IVariableGroup>
                .Empty
                .Add(new SchedulerVariableGroup
                {
                    Home = schedulerOptions.Value.Home,
                    Name = schedulerOptions.Value.Name,
                })
                .Add(new WorkflowVariableGroup(workflow.Variables.ToImmutableDictionary(x => x.Key, x => x.Value.Render([])))
                {
                    Path = workflow.Path,
                    Mode = mode,
                });

        try
        {
            if (steps.Count > 0)
            {
                about.Tick.LogInformation("Executing custom steps: {workflow.steps}.", steps);

                foreach (var name in steps)
                {
                    if (workflow.Steps.SingleOrDefault(s => name.Equals(s.Name, StringComparison.InvariantCultureIgnoreCase)) is { } step)
                    {
                        await executeStep.AsProcess(step, mode, variables, cancellationToken);
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
                        await executeStep.AsProcess(step, mode, variables, cancellationToken);
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