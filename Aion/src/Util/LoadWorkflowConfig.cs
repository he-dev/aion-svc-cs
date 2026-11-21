using System;
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
    public async Task<WorkflowConfig?> FromFileOrNull(WorkflowPath workflowPath)
    {
        using var about = logger.Baseline.BeginScopeAbout<LoadWorkflowConfig>
        ([
            ("@workflow.name", workflowPath)
        ]);
        about.Zero.Internal.LogDebug(null);

        if (!File.Exists(workflowPath))
        {
            about.LogError("Workflow config '{@workflow.name}' does not exist.");
            return null;
        }

        var config = workflowPath.Extension.ToLowerInvariant() switch
        {
            ".json" => await FromJsonOrNull(workflowPath),
            ".yaml" or ".yml" => await FromYamlOrNull(workflowPath),
            _ => throw new NotSupportedException($"Extension '{workflowPath.Extension}' is not supported.")
        };

        if (config is null)
        {
            about.Last.LogError("Workflow config '{@workflow.name}' is null or empty.");
            return null;
        }

        var configDefault = await loadWorkflowConfigDefault.FromFileOrEmpty(workflowPath);

        // core: Apply config defaults.
        config = config with
        {
            Variables = config.Variables.Concat(configDefault.Variables).LastValueWins(StringComparer.OrdinalIgnoreCase).ToDictionary(),
            Logger = config.Logger ?? configDefault.Logger
        };

        if (!validateWorkflowConfig.Invoke(config))
        {
            about.Last.LogInformation("Workflow config '{@workflow.name}' fails some validations.");
            return null;
        }

        about.Last.LogInformation("Workflow config '{@workflow.name}' passes all validations.");

        return config;
    }

    private async Task<WorkflowConfig?> FromJsonOrNull(WorkflowPath workflowPath)
    {
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
            logger.Snap<LoadWorkflowConfig>().Internal.LogError(e, "Workflow '{@workflow.name}' fails to load.", workflowPath);
            return null;
        }
    }

    private async Task<WorkflowConfig?> FromYamlOrNull(WorkflowPath workflowPath)
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
            return deserializer.Deserialize<WorkflowConfig>(contents);
        }
        catch (Exception e)
        {
            logger.Snap<LoadWorkflowConfig>().Internal.LogError(e, "Workflow '{@workflow.name}' fails to load.", workflowPath);
            return null;
        }
    }
}