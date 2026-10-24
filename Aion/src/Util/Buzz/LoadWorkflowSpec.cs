using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Meta.Logging.Wiretap;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Buzz;

public class LoadWorkflowSpec
(
    ILogger<LoadWorkflowSpec> logger,
    ValidateWorkflowFile validateWorkflowFile
)
{
    public async Task<WorkflowSpec?> FromJsonOrNull(WorkflowPath workflowPath)
    {
        using var scope = logger.BeginScopeWith(("@workflow.name", workflowPath));

        if (!File.Exists(workflowPath))
        {
            logger.System.Last.LogError("Workflow spec '{@workflow.name}' does not exist.");
            return null;
        }

        try
        {
            await using var fileStream = new FileStream(workflowPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var json = await JsonSerializer.DeserializeAsync<WorkflowSpecJson>(fileStream, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            });

            if (json is null)
            {
                logger.System.Last.LogError("Workflow '{@workflow.name}' is null or empty.");
                return null;
            }

            if (validateWorkflowFile.That(json).ToList() is { Count: > 0 } issues)
            {
                foreach (var issue in issues)
                {
                    logger.System.LogError("{workflow.issue}", issue);
                }
            }

            // note: At this point we know that the file is okay.
            var spec = new WorkflowSpec
            {
                Path = workflowPath,
                Cron = json.Cron!,
                Disabled = json.Disabled == true,
                Variables = json.Variables!.ToImmutableDictionary(x => x.Key, x => x.Value!),
                Logger = json.Logger,
                Steps =
                [
                    .. json.Steps!.Select((step, index) => new WorkflowSpec.Step
                    {
                        Index = index,
                        Name = step!.Name,
                        Disabled = step.Disabled == true,
                        FileName = step.FileName!,
                        Arguments = step.Arguments?.Select(x => x!).ToImmutableList() ?? [],
                        Environment = step.Environment?.ToImmutableDictionary(x => x.Key, x => x.Value!) ?? ImmutableDictionary<string, string>.Empty,
                        WorkingDirectory = step.WorkingDirectory,
                        Timeout = step.Timeout ?? System.Threading.Timeout.InfiniteTimeSpan
                    })
                ]
            };

            logger.System.Last.LogDebug("Workflow '{@workflow.name}' is valid.");
            return spec;
        }
        catch (Exception e)
        {
            logger.System.Last.LogError(e, "An error occurred while deserializing '{@workflow.name}'.");
            return null;
        }
    }

}