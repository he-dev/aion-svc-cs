using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Data.Configs;
using Aion.Data.Specs;
using Aion.Data.Templating;
using Microsoft.Extensions.Logging;
using Quartz;
using Wiretap.Core;

namespace Aion.Util;

public class LoadWorkflowSpec
(
    ILogger<LoadWorkflowSpec> logger,
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

        // note: Create a partial workflow first so that we can use the Mode property for variables.
        var workflow = new WorkflowSpec
        {
            Path = workflowPath,
            Trigger = CreateWorkflowTrigger.Create(workflowPath, WorkflowMode.Job, builder => builder.WithCronSchedule(config.Cron)),
            Enabled = config.Enabled,
            Variables = config.Variables.ToImmutableDictionary(x => x.Key, x => TemplateString.From(x.Value)),
            Logger = await loadLoggingPreset.FromFromOrDefault(config.Logger, global::Serilog.Core.Logger.None),
            Steps = []
        };

        var nullSteps =
            config
                .Steps
                .Select(CreateStep)
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

    private StepSpec? CreateStep(StepConfig config, int index)
    {
        try
        {
            return new StepSpec
            {
                Index = index,
                Name = config.Name,
                Enabled = config.Enabled,
                FileName = config.FileName,
                Arguments = [.. config.Arguments.Select(TemplateString.From)],
                Environment = config.Environment.ToImmutableDictionary(x => x.Key, x => TemplateString.From(x.Value)),
                WorkingDirectory = config.WorkingDirectory ?? string.Empty,
                Timeout = config.Timeout,
            };
        }
        catch (Exception ex)
        {
            logger.Internal.LogError(ex, "Cannot create step {workflow.step.index} because: {workflow.step.exception:l}.", index, ex.Message);
            return null;
        }
    }
}