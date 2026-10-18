using System;
using System.Collections.Immutable;
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
    public async Task<Workflow> From(WorkflowPath workflowPath, WorkflowPatch patch)
    {
        using var scope = logger.BeginScopeWith(("aion.workflow.path", workflowPath.Workflow));
        logger.LogTrace("Composing workflow '{aion.workflow.path}'.", workflowPath.Workflow);

        var profile = await loadWorkflowProfile.Where(workflowPath);
        var spec = await loadWorkflowSpec.FromFile(workflowPath, profile as WorkflowProfile.Okay ?? new WorkflowProfile.Okay());

        if (spec is WorkflowSpec.Okay okay)
        {
            // meta: Create a partial workflow first so that we can use the Mode property for variables.
            var workflow = new Workflow.Okay
            {
                Spec = okay,
                Enabled = !okay.Disabled || patch.Force,
                Logger = okay.Logger is null ? Serilog.Core.Logger.None : await loadLoggingPreset.Where(okay.Logger),
                Trigger = patch.Trigger ?? CreateWorkflowTrigger.Cron(spec.Path, okay.Cron),
                Variables = okay.Variables.ToImmutableDictionary(),
                Steps = []
            };

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

            workflow = workflow with
            {
                Steps = [.. okay.Steps.Select(step => CreateStep(step, variableGroups))]
            };

            if (patch.Steps.Any())
            {
                workflow = workflow with
                {
                    Steps =
                    [
                        ..
                        from step in workflow.Steps.OfType<Workflow.Step.Okay>()
                        where patch.Steps.Contains(step.Name)
                        select step
                    ]
                };
            }


            return workflow;
        }

        if (spec is WorkflowSpec.Yuck yuck)
        {
            return new Workflow.Yuck
            {
                Spec = yuck
            };
        }

        throw new InvalidOperationException($"Unsupported workflow spec: {spec.GetType().Name}");
    }

    private Workflow.Step CreateStep
    (
        WorkflowSpec.Step spec,
        IImmutableList<IVariableGroup> variables
    )
    {
        using var scope = logger.BeginScopeFrom(new { StepIndex = spec.Index });
        variables = variables.Add(new StepVariableGroup { Index = spec.Index, Name = spec.Name });
        try
        {
            return new Workflow.Step.Okay
            {
                Spec = spec,
                Enabled = !spec.Disabled,
                Index = spec.Index,
                Name = spec.Name,
                FileName = spec.FileName.Render(variables),
                Arguments = traceId =>
                {
                    variables = variables.Add(new ExecutionVariableGroup { TraceId = traceId });
                    return spec.Arguments?.Select(argument => argument.Render(variables)).ToArray() ?? [];
                },
                Environment = spec.Environment.ToImmutableDictionary(x => x.Key, x => x.Value.Render(variables)),
                WorkingDirectory = (spec.WorkingDirectory ?? string.Empty).Render(variables),
                Timeout = spec.Timeout,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to render step {StepIndex}.", spec.Index);
            return new Workflow.Step.Yuck
            {
                Spec = spec,
                Index = spec.Index,
                Issues = [$"Failed to render step: {ex.Message}"]
            };
        }
    }
}