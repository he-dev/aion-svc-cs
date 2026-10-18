using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Meta;
using Aion.Meta.Logging.Wiretap;
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
        var profile = new WorkflowProfile.Okay();
        var file = new FileInfo(workflowPath);
        for (var dir = file.Directory; dir != null; dir = dir.Parent)
        {
            // core: Stop searching when we reach the scheduler-home.
            if (ComparePaths.AreEqual(dir.FullName, workflowPath.Home))
            {
                break;
            }

            var profilePath = Path.Join(dir.FullName, ".profile.json");
            if (await FromFile(profilePath) is WorkflowProfile.Okay okay)
            {
                profile += okay;
            }
        }

        return profile;
    }

    public async Task<WorkflowProfile> FromFile(string profilePath)
    {
        using var scope = logger.BeginScopeWith(("aion.profile.path", profilePath));

        if (!File.Exists(profilePath))
        {
            logger.Buzz.LogError("Cannot load workflow-profile from '{aion.profile.path}' because it does not exist.", profilePath);
            return new WorkflowProfile.Yuck { Path = profilePath, Issues = ["The workflow-profile file does not exist."] };
        }

        try
        {
            await using var fileStream = new FileStream(profilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var profile = await JsonSerializer.DeserializeAsync<WorkflowProfileJson>(fileStream, new JsonSerializerOptions
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
                logger.Buzz.LogError("File '{aion.profile.path}' is empty. It needs to contain a workflow-profile.");
                return new WorkflowProfile.Yuck { Path = profilePath, Issues = ["The workflow-profile file is empty."] };
            }

            var yuck = new WorkflowProfile.Yuck
            {
                Path = profilePath,
                Issues = [.. validateWorkflowProfile.That(profile)]
            };

            if (yuck.Issues.Any())
            {
                foreach (var issue in yuck.Issues)
                {
                    logger.Buzz.LogError("{aion.profile.issue}", issue);
                }

                return yuck;
            }

            return new WorkflowProfile.Okay
            {
                Variables = profile.Variables?.ToImmutableDictionary(x => x.Key, x => x.Value!) ?? ImmutableDictionary<string, string>.Empty,
                Logger = profile.Logger
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while deserializing the workflow profile '{aion.profile.path}'.");
            return new WorkflowProfile.Yuck { Path = profilePath, Issues = ["An error occurred while deserializing the workflow profile."] };
        }
    }
}