using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Buzz;

public class DeserializeWorkflowSpec(ILogger<DeserializeWorkflowSpec> logger)
{
    public async Task<WorkflowSpec> FromFile(WorkflowPath workflowPath)
    {
        if (!File.Exists(workflowPath))
        {
            logger.LogError("The workflow '{WorkflowPath}' does not exist.", workflowPath);
            return WorkflowSpec.Disabled with { Path = workflowPath };
        }

        try
        {
            await using var fileStream = new FileStream(workflowPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var spec = await JsonSerializer.DeserializeAsync<WorkflowSpec>(fileStream, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            });

            if (spec is null)
            {
                logger.LogError("The workflow '{WorkflowPath}' could not be deserialized.", workflowPath);
                return WorkflowSpec.Disabled with { Path = workflowPath };
            }

            spec = spec with
            {
                Path = workflowPath,
                Steps = [.. spec.Steps.Select((step, index) => step with { Index = index })]
            };

            return spec;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while deserializing the workflow '{WorkflowPath}'.", workflowPath);
            return WorkflowSpec.Disabled with { Path = workflowPath };
        }
    }
}