using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Data.Configs;
using Aion.Data.Scheduler;
using Aion.Data.Specs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Wiretap.Core;

namespace Aion.Util;

public class LoadWorkflowSpec
(
    ILogger<LoadWorkflowSpec> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    LoadWorkflowConfig loadWorkflowConfig,

    LoadLoggingPreset loadLoggingPreset
)
{
    public async Task<WorkflowSpec?> FromFile(WorkflowPath workflowPath)
    {
        using var about = logger.Internal.BeginScopeAbout<LoadWorkflowSpec>([("@workflow.name", workflowPath)]);
        about.Zero.LogDebug("Loading workflow '{@workflow.name}'.", workflowPath);

        var config = await loadWorkflowConfig.FromFileOrNull(workflowPath);

        if (config is null)
        {
            about.Last.LogError("Cannot load workflow '{@workflow.name}' because there is no spec.");
            return null;
        }

        // meta: Create a partial workflow first so that we can use the Mode property for variables.
        var workflow = new WorkflowSpec
        {
            Path = workflowPath,
            Trigger = CreateWorkflowTrigger.Create(workflowPath, WorkflowTriggerSource.Job, builder => builder.WithCronSchedule(config.Cron)),
            Enabled = config.Enabled == true,
            Variables = config.Variables.ToImmutableDictionary(),
            Logger = await loadLoggingPreset.FromFromOrDefault(config.Logger, global::Serilog.Core.Logger.None),
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
                Path = workflowPath,
            }
        ]);

        var nullSteps =
            config
                .Steps
                .Select((step, i) => CreateStep(step, i, variableGroups))
                .ToLookup(step => step is null);

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

    private StepSpec? CreateStep
    (
        StepConfig config,
        int index,
        IImmutableList<IVariableGroup> variables
    )
    {
        variables = variables.Add(new StepVariableGroup
        {
            Index = index,
            Name = config.Name
        });

        try
        {
            var step = new StepSpec
            {
                Index = index,
                Name = config.Name,
                Enabled = config.Enabled,
                FileName = config.FileName.Render(variables),
                Arguments = (source, traceId, parentSpanId) =>
                {
                    var local = variables.Add(new ExecutionVariableGroup
                    {
                        Source = source,
                        TraceId = traceId,
                        ParentSpanId = parentSpanId
                    });
                    return [.. config.Arguments.Select(argument => argument.Render(local))];
                },
                Environment = config.Environment.ToImmutableDictionary(x => x.Key, x => x.Value.Render(variables)),
                WorkingDirectory = (config.WorkingDirectory ?? string.Empty).Render(variables),
                Timeout = config.Timeout,
            };

            // core: Execute arguments with a random trace ID to ensure that it works.
            //step.Arguments(ActivityTraceId.CreateRandom().ToString());

            return step;
        }
        catch (Exception ex)
        {
            logger.Internal.LogError(ex, "Cannot create step {workflow.step.index} because: {workflow.step.exception:l}.", index, ex.Message);
            return null;
        }
    }
}