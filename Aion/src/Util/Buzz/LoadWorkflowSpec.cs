using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;
using Wiretap.Core;

namespace Aion.Util.Buzz;

public class LoadWorkflowSpec
(
    ILogger<LoadWorkflowSpec> logger,
    ValidateWorkflowFile validateWorkflowFile
)
{
    public async Task<WorkflowSpec?> FromFileOrNull(WorkflowPath workflowPath)
    {
        using var scope = logger.BeginScopeWith(("@workflow.name", workflowPath));

        if (!File.Exists(workflowPath))
        {
            logger.With(Story.Domain).LogError("Workflow spec '{@workflow.name}' does not exist.");
            return null;
        }

        var data = workflowPath.Extension.ToLower() switch
        {
            ".json" => await FromJsonOrNull(workflowPath),
            ".yaml" or ".yml" => await FromYamlOrNull(workflowPath),
            _ => throw new NotSupportedException($"Extension '{workflowPath.Extension}' is not supported.")
        };

        if (data is null)
        {
            logger.With(Story.Domain).LogError("Workflow '{@workflow.name}' is null or empty.");
            return null;
        }

        if (validateWorkflowFile.That(data).ToList() is { Count: > 0 } issues)
        {
            foreach (var issue in issues)
            {
                logger.With(Story.Domain).LogError("{workflow.issue}", issue);
            }

            return null;
        }

        logger.With(Story.Domain).LogDebug("Workflow '{@workflow.name}' passes validations.");

        try
        {
            // note: At this point we know that the file is okay.
            return new WorkflowSpec
            {
                Path = workflowPath,
                Cron = data.Cron!,
                Disabled = data.Disabled == true,
                Variables = data.Variables?.ToImmutableDictionary(x => x.Key, x => x.Value!) ?? [],
                Logger = data.Logger,
                Steps =
                [
                    .. data.Steps!.Select((step, index) => new WorkflowSpec.Step
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
        }
        catch (Exception e)
        {
            scope.With(Story.System).LogError(e, "Workflow spec '{@workflow.name}' does not deserialize.");
            return null;
        }
    }

    private async Task<WorkflowFile?> FromJsonOrNull(WorkflowPath workflowPath)
    {
        try
        {
            await using var fileStream = new FileStream(workflowPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<WorkflowFile>(fileStream, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            });
        }
        catch (Exception e)
        {
            return null;
        }
    }

    private async Task<WorkflowFile?> FromYamlOrNull(WorkflowPath workflowPath)
    {
        try
        {
            var contents = await File.ReadAllTextAsync(workflowPath);
            var deserializer =
                new YamlDotNet
                        .Serialization
                        .DeserializerBuilder()
                    .WithCaseInsensitivePropertyMatching()
                    .Build();
            return deserializer.Deserialize<WorkflowFile>(contents);
        }
        catch (Exception e)
        {
            return null;
        }
    }
}