using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Data;
using Microsoft.Extensions.Logging;
using Wiretap.Core;

namespace Aion.Util;

public class LoadWorkflowProfile
(
    ILogger<LoadWorkflowProfile> logger,
    ValidateWorkflowProfile validateWorkflowProfile
)
{
    public async Task<WorkflowProfile> FromJsonOrEmpty(WorkflowPath workflowPath)
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
            if (await FromJsonOrNull(profilePath) is { } current)
            {
                logger.Internal.LogDebug("Merging logger-profile with '{profile.path}'.", profilePath);
                profile += current;
            }
        }

        return profile;
    }

    public async Task<WorkflowProfile?> FromJsonOrNull(string profilePath)
    {
        using var scope = logger.BeginScopeWith(("profile.path", profilePath));

        if (!File.Exists(profilePath))
        {
            logger.Internal.LogError("Cannot load workflow-profile from '{profile.path}' because it does not exist.", profilePath);
            return null;
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
                logger.Internal.LogError("File '{profile.path}' is empty. It needs to contain a workflow-profile.");
                return null;
            }

            if (validateWorkflowProfile.That(profile).ToList() is { Count: > 0 } issues)
            {
                foreach (var issue in issues)
                {
                    logger.Internal.LogError("{profile.issue}", issue);
                }

                return null;
            }

            return new WorkflowProfile
            {
                Variables = profile.Variables?.ToImmutableDictionary(x => x.Key, x => x.Value!) ?? ImmutableDictionary<string, string>.Empty,
                Logger = profile.Logger
            };
        }
        catch (Exception e)
        {
            logger.Internal.LogError(e, "File '{profile.path}' fails to deserialize as workflow-profile.");
            return null;
        }
    }
}