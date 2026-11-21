using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Data.Configs;
using Aion.Util.Dotnet;
using Microsoft.Extensions.Logging;
using Wiretap.Core;

namespace Aion.Util;

public class LoadWorkflowConfig
(
    ILogger<LoadWorkflowConfig> logger,
    LoadWorkflowConfigDefault loadWorkflowConfigDefault,
    ValidateWorkflowConfig validateWorkflowConfig
)
{
    // core: In case the config specifies explicit nulls, fallback to empty collections.
    [SuppressMessage("ReSharper", "NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract")]
    public async Task<WorkflowConfig?> FromFileOrNull(WorkflowPath workflowPath)
    {
        if (!File.Exists(workflowPath))
        {
            logger.Baseline.LogError("Workflow config '{@workflow.name}' does not exist.");
            return null;
        }

        var config = await FromJsonOrNull(workflowPath) ?? await FromYamlOrNull(workflowPath);

        if (config is null)
        {
            logger.Baseline.LogError("Workflow config '{@workflow.name}' does not load.");
            return null;
        }

        config = config with
        {
            Variables = config.Variables ?? new Dictionary<string, string>(),
            Steps =
            [
                ..
                from step in config.Steps
                select step with
                {
                    Arguments = step.Arguments ?? [],
                    Environment = step.Environment ?? new Dictionary<string, string>()
                }
            ]
        };

        var configDefault = await loadWorkflowConfigDefault.FromFileOrEmpty(workflowPath);

        // core: Apply config defaults.
        config = config with
        {
            Variables = config.Variables.Concat(configDefault.Variables).LastValueWins(StringComparer.OrdinalIgnoreCase).ToDictionary(),
            Logger = config.Logger ?? configDefault.Logger
        };

        if (!validateWorkflowConfig.Invoke(config))
        {
            logger.Baseline.LogError("Workflow config '{@workflow.name}' fails some validations.");
            return null;
        }

        logger.Baseline.LogInformation("Workflow config '{@workflow.name}' passes all validations.");

        return config;
    }

    private async Task<WorkflowConfig?> FromJsonOrNull(WorkflowPath workflowPath)
    {
        switch (Path.GetExtension(workflowPath).ToLowerInvariant())
        {
            case ".json":
                // core: This extension is supported.
                break;
            default:
                logger.Internal.LogDebug("Workflow config '{@workflow.name}' is not a JSON file.", workflowPath);
                return null;
        }

        try
        {
            await using var fileStream = new FileStream(workflowPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<WorkflowConfig>(fileStream, new JsonSerializerOptions
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
            logger.Baseline.LogError(e, "Workflow config '{@workflow.name}' fails to load.", workflowPath);
            return null;
        }
    }

    private async Task<WorkflowConfig?> FromYamlOrNull(WorkflowPath workflowPath)
    {
        switch (Path.GetExtension(workflowPath).ToLowerInvariant())
        {
            case ".yaml" or ".yml":
                // core: This extension is supported.
                break;
            default:
                logger.Internal.LogDebug("Workflow config '{@workflow.name}' is not a YAML file.", workflowPath);
                return null;
        }

        try
        {
            var contents = await File.ReadAllTextAsync(workflowPath);
            var deserializer =
                new YamlDotNet
                        .Serialization
                        .DeserializerBuilder()
                    .WithCaseInsensitivePropertyMatching()
                    .Build();
            return deserializer.Deserialize<WorkflowConfig>(contents);
        }
        catch (Exception e)
        {
            logger.Baseline.LogError(e, "Workflow config '{@workflow.name}' fails to load.", workflowPath);
            return null;
        }
    }
}