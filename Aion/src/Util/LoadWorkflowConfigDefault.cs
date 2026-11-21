using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Data.Configs;
using Microsoft.Extensions.Logging;
using Wiretap.Core;

namespace Aion.Util;

public class LoadWorkflowConfigDefault
(
    ILogger<LoadWorkflowConfigDefault> logger,
    ValidateWorkflowConfigDefault validateWorkflowConfigDefault
)
{
    private static readonly IImmutableList<string> Extensions = [".json", ".yaml", ".yml"];

    public async Task<WorkflowConfigDefault> FromFileOrEmpty(WorkflowPath workflowPath)
    {
        var configDefault = WorkflowConfigDefault.Empty;
        var file = new FileInfo(workflowPath);
        for (var dir = file.Directory; dir != null; dir = dir.Parent)
        {
            var configPaths =
                Extensions
                    .Select(ext => Path.Join(dir.FullName, $"{WorkflowConfigDefault.FileName}{ext}"))
                    .Where(File.Exists)
                    .ToList();

            switch (configPaths)
            {
                case { Count: 0 }:
                    logger.Baseline.LogDebug("Workflow config default '{workflow.config.name}' does not exist.");
                    break;

                case { Count: 1 }:
                    configDefault += await FromJsonOrNull(configPaths[0]) ?? await FromYamlOrNull(configPaths[0]) ?? WorkflowConfigDefault.Empty;
                    break;

                // note: Too many files.
                default:
                    logger.Baseline.LogError("Workflow config default '{workflow.config.name}' is ambiguous. Only one file extension is supported.");
                    break;
            }

            // core: Stop searching when we reach the scheduler-home.
            if (ComparePaths.AreEqual(dir.FullName, workflowPath.Root))
            {
                break;
            }
        }

        if (!validateWorkflowConfigDefault.That(configDefault))
        {
            return WorkflowConfigDefault.Empty;
        }

        return configDefault;
    }

    private async Task<WorkflowConfigDefault?> FromJsonOrNull(string configDefaultPath)
    {
        switch (Path.GetExtension(configDefaultPath).ToLowerInvariant())
        {
            case ".json":
                // core: This extension is supported.
                break;
            default:
                logger.Internal.LogDebug("Workflow config '{workflow.config.path}' is not a JSON file.", configDefaultPath);
                return null;
        }

        try
        {
            await using var fileStream = new FileStream(configDefaultPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<WorkflowConfigDefault>(fileStream, new JsonSerializerOptions
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
            logger.Internal.LogError(e, "Workflow config default '{workflow.config.path}' fails to load.", configDefaultPath);
        }

        return null;
    }

    private async Task<WorkflowConfigDefault?> FromYamlOrNull(string configDefaultPath)
    {
        switch (Path.GetExtension(configDefaultPath).ToLowerInvariant())
        {
            case ".yaml" or ".yml":
                // core: This extension is supported.
                break;
            default:
                logger.Internal.LogDebug("Workflow config '{workflow.config.path}' is not a YAML file.", configDefaultPath);
                return null;
        }

        try
        {
            var contents = await File.ReadAllTextAsync(configDefaultPath);
            var deserializer =
                new YamlDotNet
                        .Serialization
                        .DeserializerBuilder()
                    .WithCaseInsensitivePropertyMatching()
                    .Build();
            return deserializer.Deserialize<WorkflowConfigDefault>(contents);
        }
        catch (Exception e)
        {
            logger.Internal.LogError(e, "Workflow config default '{workflow.config.path}' fails to load.", configDefaultPath);
            return null;
        }
    }
}