using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Data;
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

        if (await loadWorkflowConfig.FromFileOrNull(workflowPath) is not { } config)
        {
            about.Last.LogError("Cannot load workflow '{@workflow.name}' because there is no spec.");
            return null;
        }

        try
        {
            return new WorkflowSpec
            {
                Path = workflowPath,
                Trigger = CreateWorkflowTrigger.Create(workflowPath, WorkflowMode.Job, builder => builder.WithCronSchedule(config.Cron)),
                Enabled = config.Enabled,
                Variables = config.Variables.ToImmutableDictionary(x => x.Key, x => TemplateString.From(x.Value)),
                Logger = await loadLoggingPreset.FromFileOrDefault(config.Logger),
                Steps =
                [
                    .. config.Steps.Select((step, index) => new StepSpec
                    {
                        Index = index,
                        Name = step.Name,
                        Enabled = config.Enabled,
                        FileName = step.FileName,
                        Arguments = [.. step.Arguments.Select(TemplateString.From)],
                        Environment = step.Environment.ToImmutableDictionary(x => x.Key, x => TemplateString.From(x.Value)),
                        WorkingDirectory = step.WorkingDirectory ?? string.Empty,
                        Timeout = step.Timeout,
                    })
                ]
            };
        }
        catch (Exception ex)
        {
            about.Last.LogError(ex, "Loading workflow '{@workflow.name}' fails.", workflowPath);
            return null;
        }
        finally
        {
            about.Last.WhenFirstOnly.LogInformation("Workflow '{@workflow.name}' loads.", workflowPath);
        }
    }
}