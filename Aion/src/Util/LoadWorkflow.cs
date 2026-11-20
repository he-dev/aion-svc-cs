using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Wiretap.Core;

namespace Aion.Util;

public class LoadWorkflow
(
    ILogger<LoadWorkflow> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    LoadWorkflowSpec loadWorkflowSpec,
    LoadWorkflowProfile loadWorkflowProfile,
    LoadLoggingPreset loadLoggingPreset
)
{
    public async Task<Workflow?> FromFile(WorkflowPath workflowPath)
    {
        using var about = logger.Internal.BeginScopeAbout<LoadWorkflow>([("@workflow.name", workflowPath)]);
        about.Zero.LogDebug("Loading workflow '{@workflow.name}'.", workflowPath);

        var spec = await loadWorkflowSpec.FromFileOrNull(workflowPath);

        if (spec is null)
        {
            about.Last.LogError("Cannot load workflow '{@workflow.name}' because there is no spec.");
            return null;
        }

        spec += await loadWorkflowProfile.FromJsonOrEmpty(workflowPath);

        // meta: Create a partial workflow first so that we can use the Mode property for variables.
        var workflow = new Workflow
        {
            Spec = spec,
            Enabled = !spec.Disabled,
            Logger = await loadLoggingPreset.FromFromOrDefault(spec.Logger, global::Serilog.Core.Logger.None),
            Trigger = CreateWorkflowTrigger.Create(spec.Path, WorkflowTriggerSource.Job, builder => builder.WithCronSchedule(spec.Cron)),
            Variables = spec.Variables.ToImmutableDictionary(),
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
            }
        ]);

        var nullSteps = spec.Steps.Select(step => CreateStep(step, variableGroups)).ToLookup(step => step is null);
        ;

        if (nullSteps[true].Any())
        {
            about.LogError("Cannot use workflow '{@workflow.name}' because it contains {workflow.step.count.invalid} invalid steps.", workflowPath, nullSteps[true].Count());
            return null;
        }

        workflow = workflow with
        {
            Steps = [.. nullSteps[false].Select(s => s!)]
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
                Arguments = (source, traceId, parentSpanId) =>
                {
                    var local = variables.Add(new ExecutionVariableGroup
                    {
                        Source = source,
                        TraceId = traceId,
                        ParentSpanId = parentSpanId
                    });
                    return [.. spec.Arguments.Select(argument => argument.Render(local))];
                },
                Environment = spec.Environment.ToImmutableDictionary(x => x.Key, x => x.Value.Render(variables)),
                WorkingDirectory = (spec.WorkingDirectory ?? string.Empty).Render(variables),
                Timeout = spec.Timeout,
            };

            // core: Execute arguments with a random trace ID to ensure that it works.
            //step.Arguments(ActivityTraceId.CreateRandom().ToString());

            return step;
        }
        catch (Exception ex)
        {
            logger.Internal.LogError(ex, "Cannot create step {workflow.step.index} because: {workflow.step.exception:l}.", spec.Index, ex.Message);
            return null;
        }
    }
}