using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Buzz;

public class LoadWorkflowSpec(ILogger<LoadWorkflowSpec> logger, IServiceProvider serviceProvider)
{
    public async Task<WorkflowSpec> FromFile(WorkflowPath workflowPath, Func<WorkflowSpec, WorkflowSpec> setDefaults)
    {
        if (!File.Exists(workflowPath))
        {
            logger.LogError("The workflow '{WorkflowPath}' does not exist.", workflowPath);
            return new WorkflowSpec { Path = workflowPath };
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
                logger.LogError("The workflow '{aion.workflow.path}' could not be deserialized.", workflowPath.Workflow);
                return new WorkflowSpec { Path = workflowPath };
            }

            spec = setDefaults(spec);

            if (ValidateObject.That(spec, serviceProvider) is { Count: > 0 } results)
            {
                logger.LogError("The workflow '{aion.workflow.path}' is invalid.", workflowPath.Workflow);
                foreach (var result in results)
                {
                    logger.LogError(result.ErrorMessage);
                }

                return new WorkflowSpec { Path = workflowPath };
            }

            spec = spec with
            {
                Path = workflowPath,
                Steps = [.. spec.Steps.Select((step, index) => step with { Index = index })]
            };

            foreach (var step in spec.Steps)
            {
                if (ValidateObject.That(step) is { Count: > 0 } stepResults)
                {
                    logger.LogError("The workflow step '{aion.workflow.path}[{aion.step.index}]' is invalid.", workflowPath.Workflow, step.Index);
                    foreach (var result in stepResults)
                    {
                        logger.LogError(result.ErrorMessage);
                        //logger.LogError("Validation '{aion.workflow.path}[{aion.step.index}]': {aion.step.validation_error}", workflowPath.Workflow, step.Index, result.ErrorMessage);
                    }

                    return new WorkflowSpec { Path = workflowPath };
                }
            }

            return spec;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while deserializing the workflow '{WorkflowPath}'.", workflowPath);
            return new WorkflowSpec { Path = workflowPath };
        }
    }
}