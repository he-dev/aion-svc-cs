using System;
using System.IO;
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
    public async Task<WorkflowConfigDefault> FromFileOrEmpty(WorkflowPath workflowPath)
    {
        var profile = WorkflowConfigDefault.Empty;
        var file = new FileInfo(workflowPath);
        for (var dir = file.Directory; dir != null; dir = dir.Parent)
        {
            var profilePath = Path.Join(dir.FullName, ".profile.json");
            if (await FromJsonOrNull(profilePath) is { } current)
            {
                logger.Internal.LogDebug("Merging logger-profile with '{profile.path}'.", profilePath);
                profile += current;
            }

            // core: Stop searching when we reach the scheduler-home.
            if (ComparePaths.AreEqual(dir.FullName, workflowPath.Root))
            {
                break;
            }
        }

        return profile;
    }

    private async Task<WorkflowConfigDefault?> FromJsonOrNull(string profilePath)
    {
        using var scope = logger.BeginScopeWith(("workflow.profile.name", Path.GetFileNameWithoutExtension(profilePath)));

        if (!File.Exists(profilePath))
        {
            logger.Baseline.LogError("Workflow profile '{workflow.profile.name}' does not exist.");
            return null;
        }

        try
        {
            await using var fileStream = new FileStream(profilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var configDefault = await JsonSerializer.DeserializeAsync<Data.Configs.WorkflowConfigDefault>(fileStream, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            });

            if (configDefault is null)
            {
                logger.Baseline.LogError("Workflow profile '{workflow.profile.name}' is empty.");
                return null;
            }

            if (!validateWorkflowConfigDefault.That(configDefault))
            {
                return null;
            }

            return configDefault;
        }
        catch (Exception e)
        {
            logger.Internal.LogError(e, "Workflow profile '{workflow.profile.name}' fails to deserialize as workflow-profile.");
            return null;
        }
    }
}