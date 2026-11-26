using System;
using System.Collections.Generic;
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
    CreateLogger createLogger,
    ForkLogging forkLogging,
    ExecuteStep executeStep
)
{
    public async Task Invoke(WorkflowSpec workflow, WorkflowMode mode, IReadOnlyList<string> customSteps, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        using var about = logger.Baseline.BeginScopeAbout<ExecuteWorkflow>
        ([
            ("workflow.name", workflow.Name),
            ("workflow.mode", mode)
        ]);
        about.Zero.LogDebug(null);

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        switch (mode)
        {
            case WorkflowMode.Job:
                if (!workflow.Enabled)
                {
                    about.Tick.LogError("Workflow '{workflow.name}' is disabled.", workflow.Name);
                    await cancellationTokenSource.CancelAsync();
                }

                if (workflow.Steps.Count == 0)
                {
                    about.Tick.LogError("Workflow '{workflow.name}' has no steps.", workflow.Name);
                    await cancellationTokenSource.CancelAsync();
                }

                break;
            case WorkflowMode.Cli:
            case WorkflowMode.Api:
            default:
                // core: There is nothing to handle in this case.
                break;
        }

        cancellationTokenSource.Token.ThrowIfCancellationRequested();

        var variables = ImmutableList<IVariableGroup>.Empty.AddRange
        ([
            new SchedulerVariableGroup
            {
                Home = schedulerOptions.Value.Home,
                Name = schedulerOptions.Value.Name,
            }
        ]);

        variables = variables.AddRange
        ([
            new WorkflowVariableGroup(workflow.Variables.ToImmutableDictionary(x => x.Key, x => RenderTemplate.String(x.Value, variables)))
            {
                Name = workflow.Name,
                Mode = mode,
            }
        ]);

        var workflowLogger = await workflow.Logger(variables);
        var workflowFork = new LoggerFork(createLogger.FromJsonOrDefault(workflowLogger) ?? global::Serilog.Core.Logger.None);
        using var unsubscribe = forkLogging.Into(workflowFork);

        try
        {
            if (customSteps.Count > 0)
            {
                about.Tick.LogInformation("Executing custom steps: {workflow.steps}.", customSteps);

                foreach (var name in customSteps)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    if (workflow.Steps.SingleOrDefault(s => name.Equals(s.Name, StringComparison.InvariantCultureIgnoreCase)) is { } step)
                    {
                        await executeStep.AsProcess(step with { Enabled = true }, variables, cancellationTokenSource.Token);
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
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await executeStep.AsProcess(step, variables, cancellationToken);
                }
            }

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