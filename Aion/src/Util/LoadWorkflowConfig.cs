using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    ValidateWorkflowConfig validateWorkflowConfig,
    ValidateWorkflowConfigDefault validateWorkflowConfigDefault
)
{
    private static readonly IImmutableList<string> Extensions = [".json", ".yaml", ".yml"];

    // core: In case the config specifies explicit nulls, fallback to empty collections.
    [SuppressMessage("ReSharper", "NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract")]
    public async Task<WorkflowConfig?> FromFileOrNull(WorkflowPath workflowPath)
    {
        var extension = Path.GetExtension(workflowPath).ToLowerInvariant();
        if (!Extensions.Contains(extension))
        {
            logger.Baseline.LogError("Workflow config '{@workflow.name}' has an invalid extension: {workflow.file.extension}.", workflowPath.Name, extension);
            return null;
        }

        if (!File.Exists(workflowPath))
        {
            logger.Baseline.LogError("Workflow config '{@workflow.name}' does not exist.");
            return null;
        }

        var configs = await Task.WhenAll
        (
            FromJsonOrNull<WorkflowConfig>(workflowPath),
            FromYamlOrNull<WorkflowConfig>(workflowPath)
        );

        // core: There can be only one file.
        if (configs.SingleOrDefault(x => x is not null) is not { } config)
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

        var configDefault = await Default(workflowPath);

        // core: Apply config defaults.
        config = config with
        {
            Variables = configDefault.Variables.Concat(config.Variables).LastValueWins(StringComparer.OrdinalIgnoreCase).ToDictionary(),
            Logger = config.Logger ?? configDefault.Logger
        };

        if (!validateWorkflowConfig.Invoke(config))
        {
            logger.Baseline.LogError("Workflow config '{@workflow.name}' fails some validations.");
            return null;
        }

        try
        {
            return config;
        }
        finally
        {
            logger.Baseline.LogInformation("Workflow config '{@workflow.name}' passes all validations.");
        }
    }

    public async Task<WorkflowConfigDefault> Default(WorkflowPath workflowPath)
    {
        var configDefault = WorkflowConfigDefault.Empty;
        var rootDepth = Path.GetFullPath(workflowPath.Root).Depth();
        var directory = Path.GetDirectoryName(workflowPath)!;
        do
        {
            var configPaths =
                Extensions
                    .Select(ext => Path.Join(directory, $"{WorkflowConfigDefault.FileName}{ext}"))
                    .Where(File.Exists)
                    .ToList();

            switch (configPaths)
            {
                case { Count: 0 }:
                    logger.Baseline.LogDebug("Workflow config default '{workflow.config.name}' does not exist.");
                    break;

                case { Count: 1 }:
                    var results = await Task.WhenAll
                    (
                        FromJsonOrNull<WorkflowConfigDefault>(configPaths[0]),
                        FromYamlOrNull<WorkflowConfigDefault>(configPaths[0])
                    );
                    // note: There can only be one file, at this point.
                    configDefault += results.Single(x => x is not null)!;
                    break;

                // note: Too many files.
                default:
                    logger.Baseline.LogError("Workflow config default '{workflow.config.name}' is ambiguous. Only one file extension is supported.");
                    break;
            }

            directory = Path.GetDirectoryName(directory)!;

            // core: Stop searching when we reach the root directory.
        } while (directory.Depth() >= rootDepth);

        return
            validateWorkflowConfigDefault.That(configDefault)
            ? configDefault
            : WorkflowConfigDefault.Empty;
    }

    private async Task<T?> FromJsonOrNull<T>(string path) where T : class
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".json":
                // core: This extension is supported.
                break;
            default:
                logger.Internal.LogDebug("Skipping unsupported type: '{workflow.config.path}'", path);
                return null;
        }

        try
        {
            await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<T>(fileStream, new JsonSerializerOptions
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
            logger.Baseline.LogError(e, "Workflow config '{workflow.config.path}' fails to load.", path);
            return null;
        }
    }

    private async Task<T?> FromYamlOrNull<T>(string path) where T : class
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".yaml" or ".yml":
                // core: This extension is supported.
                break;
            default:
                logger.Internal.LogDebug("Skipping unsupported type: '{workflow.config.path}'", path);
                return null;
        }

        try
        {
            var contents = await File.ReadAllTextAsync(path);
            var deserializer =
                new YamlDotNet
                        .Serialization
                        .DeserializerBuilder()
                    .WithCaseInsensitivePropertyMatching()
                    .Build();
            return deserializer.Deserialize<T>(contents);
        }
        catch (Exception e)
        {
            logger.Baseline.LogError(e, "Workflow config '{workflow.config.path}' fails to load.", path);
            return null;
        }
    }
}