using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Buzz;

public class DeserializeWorkflowProfile(ILogger<DeserializeWorkflowProfile> logger)
{
    public async Task<WorkflowProfile> FromFile(string workflowPath)
    {
        if (!File.Exists(workflowPath))
        {
            logger.LogError("The workflow profile '{WorkflowPath}' does not exist.", workflowPath);
            return WorkflowProfile.Empty;
        }

        try
        {
            await using var fileStream = new FileStream(workflowPath, FileMode.Open, FileAccess.Read, FileShare.Read);
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
                logger.LogError("The workflow profile '{WorkflowPath}' could not be deserialized.", workflowPath);
            }

            return profile ?? WorkflowProfile.Empty;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while deserializing the workflow profile '{WorkflowPath}'.", workflowPath);
            return WorkflowProfile.Empty;
        }
    }
}