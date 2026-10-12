using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Meta;
using Aion.Meta.Logging;
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

        var profile = await loadWorkflowProfile.FromFile(workflowPath);
        var spec = await loadWorkflowSpec.FromFile(workflowPath, temp => temp with
        {
            Variables = (profile.Variables ?? new()).Concat(temp.Variables ?? new()).KeepLast().ToDictionary(),
            Logger = temp.Logger?.Equals("@profile", StringComparison.OrdinalIgnoreCase) == true ? profile.Logger : temp?.Logger
        });

         if (spec is WorkflowSpec.Null)
         {
             return new Workflow { Disabled = true, Path = spec.Path };
         }

        // meta: Create a partial workflow first so that we can use the Mode property for variables.
        var workflow = new Workflow
        {
            Disabled = spec.Disabled,
            Path = spec.Path,
            Logger = spec.Logger is null ? Serilog.Core.Logger.None : await loadLoggingPreset.Where(spec.Logger),
            Trigger = patch.Trigger ?? CreateWorkflowTrigger.Cron(spec.Path, spec.Cron),
            Variables = (spec.Variables ?? new()).ToImmutableDictionary()
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

        var executableSteps = spec.Steps.Where(step => step.Disabled);
        if (patch.Steps.Any())
        {
            executableSteps = from selected in patch.Steps
                from candidate in spec.Steps
                where candidate.Name?.Equals(selected, StringComparison.OrdinalIgnoreCase) == true
                select candidate;
        }

        try
        {
            workflow = workflow with
            {
                Steps = [.. executableSteps.Select(step => CreateStep(step, variableGroups)).Where(step => !step.Disabled)],
            };

            switch (workflow.Steps.Count)
            {
                case 0:
                    logger.LogError("Workflow '{WorkflowPath}' has no executable steps.", spec.Path);
                    break;
                case 1:
                    logger.LogDebug("Workflow '{WorkflowPath}' has 1 executable step.", spec.Path);
                    break;
                default:
                    logger.LogDebug("Workflow '{WorkflowPath}' has {ExecutableStepCount} executable steps.", spec.Path, workflow.Steps.Count);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create workflow from '{WorkflowPath}'.", spec.Path);
            workflow = new Workflow { Path = spec.Path, Exception = ex };
        }

        return workflow;
    }

    private Workflow.Step CreateStep
    (
        WorkflowStepSpec spec,
        IImmutableList<IVariableGroup> variables
    )
    {
        using var scope = logger.BeginScopeFrom(new { StepIndex = spec.Index });
        variables = variables.Add(new StepVariableGroup { Index = spec.Index, Name = spec.Name });
        try
        {
            return new Workflow.Step
            {
                Index = spec.Index,
                Name = spec.Name,
                FileName = spec.FileName.Render(variables),
                Arguments = () => spec.Arguments?.Select(argument => argument.Render(variables)).ToArray() ?? [],
                Environment = (spec.Environment ?? new Dictionary<string, string>()).ToImmutableDictionary(x => x.Key, x => x.Value.Render(variables)),
                WorkingDirectory = (spec.WorkingDirectory ?? string.Empty).Render(variables),
                Timeout = spec.Timeout ?? System.Threading.Timeout.InfiniteTimeSpan,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to render step {StepIndex}.", spec.Index);
            return new Workflow.Step { Index = spec.Index, Exception = ex };
        }
    }
}