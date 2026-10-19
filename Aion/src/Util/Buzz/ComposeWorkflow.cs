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
    public async Task<Workflow?> From(WorkflowPath workflowPath, WorkflowPatch patch)
    {
        using var scope = logger.BeginScopeWith(("aion.workflow.path", workflowPath.Workflow));
        logger.LogTrace("Composing workflow '{aion.workflow.path}'.", workflowPath.Workflow);

        var spec = await loadWorkflowSpec.FromJson(workflowPath);

        if (spec is null)
        {
            return null;
        }

        spec += await loadWorkflowProfile.WhereOrEmpty(workflowPath);

        // meta: Create a partial workflow first so that we can use the Mode property for variables.
        var workflow = new Workflow
        {
            Spec = spec,
            Enabled = !spec.Disabled,
            Logger = spec.Logger is null ? Serilog.Core.Logger.None : await loadLoggingPreset.Where(spec.Logger),
            Trigger = CreateWorkflowTrigger.Cron(spec.Path, spec.Cron),
            Variables = spec.Variables.ToImmutableDictionary(),
            Steps = []
        } + patch;

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
                Mode = workflow.Mode,
            }
        ]);

        var steps = spec.Steps.Select(step => CreateStep(step, variableGroups)).ToLookup(step => step is null);

        if (steps[true].Any())
        {
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
            logger.Buzz.LogError(ex, "Failed to create step {aion.workflow.step.index}: {aion.workflow.step.exception:l}.", spec.Index, ex.Message);
            return null;
        }
    }
}