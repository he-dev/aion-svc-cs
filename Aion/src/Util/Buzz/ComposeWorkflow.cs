using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Meta.Logging.Wiretap;
using Aion.Util.Data;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Util.Buzz;

public class ComposeWorkflow
(
    ILogger<ComposeWorkflow> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    LoadWorkflowSpec loadWorkflowSpec,
    LoadWorkflowProfile loadWorkflowProfile,
    LoadLoggingPreset loadLoggingPreset
)
{
    public async Task<Workflow?> From(WorkflowPath workflowPath, WorkflowMod? mod = null)
    {
        using var scope = logger.BeginScopeWith(("@workflow.name", workflowPath));
        logger.LogDebug("Composing workflow '{@workflow.name}'.", workflowPath);

        var spec = await loadWorkflowSpec.FromJsonOrNull(workflowPath);

        if (spec is null)
        {
            logger.System.Halt.LogDebug("Cannot load workflow '{@workflow.name}' because there is no spec.");
            return null;
        }

        spec += await loadWorkflowProfile.FromJsonOrEmpty(workflowPath);

        // meta: Create a partial workflow first so that we can use the Mode property for variables.
        var workflow = new Workflow
        {
            Spec = spec,
            Enabled = !spec.Disabled,
            Logger = await loadLoggingPreset.FromJsonOrDefault(spec.Logger, Serilog.Core.Logger.None),
            Trigger = CreateWorkflowTrigger.Cron(spec.Path, spec.Cron),
            Variables = spec.Variables.ToImmutableDictionary(),
            Steps = []
        } + mod;

        var variableGroups = ImmutableList<IVariableGroup>.Empty.AddRange
        ([
            new SchedulerVariableGroup
            {
                Home = schedulerOptions.Value.Home,
                Name = schedulerOptions.Value.Name,
            },
            new WorkflowVariableGroup(workflow.Variables)
            {
                Path = spec.Path,
                TriggerMode = workflow.TriggerMode,
            }
        ]);

        var steps = spec.Steps.Select(step => CreateStep(step, variableGroups)).ToLookup(step => step is null);

        if (steps[true].Any())
        {
            logger.System.Halt.LogDebug("Cannot load workflow '{@workflow.name}' because it contains {workflow.step.count.invalid} invalid steps.", workflowPath, steps[true].Count());
            return null;
        }

        workflow = workflow with
        {
            Steps = [.. steps[false].Select(s => s!)]
        };

        return workflow;
    }

    private Workflow.Step? CreateStep
    (
        WorkflowSpec.Step spec,
        IImmutableList<IVariableGroup> variables
    )
    {
        variables = variables.Add(new StepVariableGroup { Index = spec.Index, Name = spec.Name });

        try
        {
            var step = new Workflow.Step
            {
                Spec = spec,
                Enabled = !spec.Disabled,
                Index = spec.Index,
                Name = spec.Name,
                FileName = spec.FileName.Render(variables),
                Arguments = traceId =>
                {
                    var local = variables.Add(new ExecutionVariableGroup { TraceId = traceId });
                    return [.. spec.Arguments.Select(argument => argument.Render(local))];
                },
                Environment = spec.Environment.ToImmutableDictionary(x => x.Key, x => x.Value.Render(variables)),
                WorkingDirectory = (spec.WorkingDirectory ?? string.Empty).Render(variables),
                Timeout = spec.Timeout,
            };

            // core: Execute arguments with a random trace ID to ensure that it works.
            step.Arguments(ActivityTraceId.CreateRandom().ToString());

            return step;
        }
        catch (Exception ex)
        {
            logger.System.Oops.LogError(ex, "Failed to create step {workflow.step.index}: {workflow.step.exception:l}.", spec.Index, ex.Message);
            return null;
        }
    }
}