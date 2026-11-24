using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Data.Configs;
using Aion.Data.Scheduler;
using Aion.Data.Specs;
using Aion.Data.Templating;
using Aion.Util.Dotnet;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiretap.Core;

namespace Aion.Util;

public class LoadWorkflow
(
    ILogger<LoadWorkflow> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    ValidateWorkflowConfig validateWorkflowConfig,
    ValidateWorkflowConfigDefault validateWorkflowConfigDefault
)
{
    private static readonly IImmutableList<string> Extensions = [".json", ".yaml", ".yml"];

    public async Task<WorkflowSpec?> Spec(WorkflowPath workflowPath)
    {
        using var about = logger.Internal.BeginScopeAbout<LoadWorkflow>([("@workflow.name", workflowPath)]);
        about.Zero.LogDebug("Loading workflow '{@workflow.name}'.", workflowPath);

        if (await Config(workflowPath) is not { } config)
        {
            about.Last.LogError("Cannot load workflow '{@workflow.name}' because there is no spec.");
            return null;
        }

        try
        {
            return new WorkflowSpec
            {
                Path = workflowPath,
                Cron = config.Cron,
                Enabled = config.Enabled,
                Variables = config.Variables.ToImmutableDictionary(x => x.Key, x => TemplateString.From(x.Value)),
                Logger = async variables =>
                {
                    var loggerPath = RenderTemplate.String(config.Logger, variables);
                    var loggerJson = await Logger(loggerPath);
                    return loggerJson.ForEachPath(path => RenderTemplate.String(path, variables));
                },
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

    // core: In case the config specifies explicit nulls, fallback to empty collections.
    [SuppressMessage("ReSharper", "NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract")]
    public async Task<WorkflowConfig?> Config(WorkflowPath workflowPath)
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

        var configDefault = await ConfigDefault(workflowPath);

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

    public async Task<WorkflowConfigDefault> ConfigDefault(WorkflowPath workflowPath)
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

    public async Task<JsonObject?> Logger(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            logger.Baseline.LogDebug("Logging preset path is null or empty. Using the none-logger.");
        }
        else
        {
            if (!Path.IsPathRooted(path))
            {
                logger.Baseline.LogDebug("Logging-preset path '{logging.preset.name}' is a name.", path);
                path = Path.Combine(schedulerOptions.Value.Home, "loggers", path);
            }

            if (Path.Exists(path))
            {
                return await FromJsonOrNull<JsonObject>(path);
            }

            logger.Baseline.LogError("Logging preset '{logging.preset.path}' does not exist.", path);
        }

        return null;
    }

    private async Task<T?> FromJsonOrNull<T>(string path) where T : class
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".json":
                // core: This extension is supported.
                break;
            default:
                logger.Internal.LogDebug("Skipping unsupported type: '{path}'", path);
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
                logger.Internal.LogDebug("Skipping unsupported type: '{path}'", path);
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