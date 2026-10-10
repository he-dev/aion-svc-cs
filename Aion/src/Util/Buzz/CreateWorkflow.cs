using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Meta;
using Aion.Meta.Logging;
using Aion.Util.Data;
using Aion.Util.Data.Scheduler;
using Aion.Util.Data.Templating.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Util.Buzz;

public class CreateWorkflow
(
    ILogger<CreateWorkflow> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    GetLoggingPreset getLoggingPreset
)
{
    public async Task<Workflow> From(WorkflowSpec spec, WorkflowProfile profile, WorkflowPatch patch)
    {
        using var scope = logger.BeginScopeFrom(new { spec.Path });
        logger.LogTrace("Rendering workflow.");

        try
        {
            CronExpression.ValidateExpression(spec.Cron);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Workflow '{WorkflowPath}' specifies an invalid cron expression: '{CronExpression}'", spec.Path, spec.Cron);
            return Workflow.Disabled with { Path = spec.Path, Exception = e };
        }

        var loggerName = spec.Logger;
        if (loggerName?.Trim().Equals("@profile", StringComparison.OrdinalIgnoreCase) == true)
        {
            loggerName = profile.Logger;
        }

        // meta: Create a partial workflow first so that we can use the Mode property for variables.
        var workflow = new Workflow
        {
            Enabled = spec.Enabled,
            Path = spec.Path,
            Logger = loggerName is null ? Serilog.Core.Logger.None : await getLoggingPreset.Where(loggerName),
            Trigger = patch.Trigger ?? CreateTrigger.Cron(spec.Path, spec.Cron),
            Variables = (profile.Variables ?? new()).Concat(spec.Variables ?? new()).KeepLast().ToImmutableDictionary()
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

        var executableSteps = spec.Steps.Where(step => step.Enabled);
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
                Steps = [.. executableSteps.Select(step => CreateStep(step, variableGroups)).Where(step => step.Enabled)],
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
            workflow = Workflow.Disabled with { Path = spec.Path, Exception = ex };
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
                Enabled = true,
                FileName = spec.FileName.Render(variables),
                Arguments = () => spec.Arguments?.Select(argument => argument.RenderValues(variables)) ?? [],
                Environment = (spec.Environment ?? new Dictionary<string, string>()).ToImmutableDictionary(x => x.Key, x => x.Value.Render(variables)),
                WorkingDirectory = (spec.WorkingDirectory ?? string.Empty).Render(variables),
                Timeout = spec.Timeout ?? System.Threading.Timeout.InfiniteTimeSpan,
                OnError = spec.OnError ?? "continue"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to render step {StepIndex}.", spec.Index);
            return Workflow.Step.Disabled with { Index = spec.Index, Exception = ex };
        }
    }
}