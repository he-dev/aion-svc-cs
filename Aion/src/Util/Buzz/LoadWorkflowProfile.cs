using System;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Meta;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Buzz;

public class LoadWorkflowProfile(ILogger<LoadWorkflowProfile> logger, IServiceProvider serviceProvider)
{
    public async Task<WorkflowProfile> Where(WorkflowPath workflowPath)
    {
        var profile = WorkflowProfile.Dummy;
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
        if (!File.Exists(profilePath))
        {
            logger.LogError("The workflow profile '{aion.profile.path}' does not exist.", profilePath);
            return WorkflowProfile.Dummy;
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
                logger.LogError("The workflow profile '{aion.profile.path}' could not be deserialized.", profilePath);
                return WorkflowProfile.Dummy;
            }

            if (ValidateObject.That(profile, serviceProvider) is { Count: > 0 } results)
            {
                logger.LogError("The workflow profile '{aion.profile.path}' is invalid.", profilePath);
                foreach (var result in results)
                {
                    logger.LogError(result.ErrorMessage);
                }

                return WorkflowProfile.Dummy;
            }

            return profile;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while deserializing the workflow profile '{aion.profile.path}'.", profilePath);
            return WorkflowProfile.Dummy;
        }
    }
}