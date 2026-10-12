using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Meta;
using Aion.Meta.Logging;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Buzz;

public class LoadWorkflowProfile
(
    ILogger<LoadWorkflowProfile> logger,
    ValidateWorkflowProfile validateWorkflowProfile
)
{
    public async Task<WorkflowProfile> Where(WorkflowPath workflowPath)
    {
        var profile = WorkflowProfile.Empty;
        var file = new FileInfo(workflowPath);
        for (var dir = file.Directory; dir != null; dir = dir.Parent)
        {
            // core: Stop searching when we reach the scheduler-home.
            if (ComparePaths.AreEqual(dir.FullName, workflowPath.Home))
            {
                break;
            }

            var profilePath = Path.Join(dir.FullName, ".profile.json");
            profile += await FromFile(profilePath);
        }

        return profile;
    }

    public async Task<WorkflowProfile> FromFile(string profilePath)
    {
        using var scope = logger.BeginScopeWith(("aion.profile.path", profilePath));

        if (!File.Exists(profilePath))
        {
            logger.LogError("Cannot load workflow-profile from '{aion.profile.path}' because it does not exist.", profilePath);
            return WorkflowProfile.Empty;
        }

        try
        {
            await using var fileStream = new FileStream(profilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var profile = await JsonSerializer.DeserializeAsync<WorkflowProfile>(fileStream, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            });

            if (profile is null)
            {
                logger.LogError("File '{aion.profile.path}' is empty. It needs to contain a workflow-profile.", profilePath);
                return WorkflowProfile.Empty;
            }

            var hasErrors = false;
            foreach (var profileIssue in validateWorkflowProfile.That(profile))
            {
                logger.LogError(profileIssue);
                hasErrors = true;
            }

            if (hasErrors)
            {
                return WorkflowProfile.Empty;
            }

            return profile;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while deserializing the workflow profile '{aion.profile.path}'.", profilePath);
            return WorkflowProfile.Empty;
        }
    }
}