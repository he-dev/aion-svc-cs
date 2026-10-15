using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Buzz;

public class LoadWorkflowSpec
(
    ILogger<LoadWorkflowSpec> logger,
    ValidateWorkflowSpec validateWorkflowSpec,
    ValidateWorkflowStepSpec validateWorkflowStepSpec
)
{
    public async Task<WorkflowSpec> FromFile(WorkflowPath workflowPath, Func<WorkflowSpec, WorkflowSpec> setDefaults)
    {
        using var scope = logger.BeginScopeWith(("aion.workflow.path", workflowPath.Workflow));

        if (!File.Exists(workflowPath))
        {
            logger.Buzz.Noop.LogError("The workflow '{aion.workflow.path}' does not exist.", workflowPath.Workflow);
            return new WorkflowSpec.Null { Path = workflowPath };
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
                logger.Buzz.Fail.LogError("The workflow '{aion.workflow.path}' could not be deserialized.", workflowPath.Workflow);
                return new WorkflowSpec.Null { Path = workflowPath };
            }

            spec = setDefaults(spec);

            var hasErrors = false;
            foreach (var workflowIssue in validateWorkflowSpec.That(spec))
            {
                logger.Buzz.Fail.LogError("{aion.workflow.issue}", workflowIssue);
                hasErrors = true;
            }

            if (hasErrors)
            {
                return new WorkflowSpec.Null { Path = workflowPath };
            }

            spec = spec with
            {
                Path = workflowPath,
                Steps = [.. spec.Steps.Select((step, index) => step with { Index = index })]
            };

            foreach (var stepIssue in spec.Steps.SelectMany(validateWorkflowStepSpec.That))
            {
                logger.LogError(stepIssue);
                hasErrors = true;
            }

            if (hasErrors)
            {
                return new WorkflowSpec.Null { Path = workflowPath };
            }

            return spec;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while deserializing the workflow '{WorkflowPath}'.", workflowPath);
            return new WorkflowSpec.Null { Path = workflowPath };
        }
    }
}