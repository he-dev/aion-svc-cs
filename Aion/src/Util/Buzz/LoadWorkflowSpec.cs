using System;
using System.Collections.Generic;
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

public class LoadWorkflowSpec
(
    ILogger<LoadWorkflowSpec> logger,
    ValidateWorkflowFile validateWorkflowFile
)
{
    public async Task<WorkflowSpec> FromFile(WorkflowPath workflowPath, WorkflowProfile.Okay profile)
    {
        using var scope = logger.BeginScopeWith(("aion.workflow.path", workflowPath.Workflow));

        if (!File.Exists(workflowPath))
        {
            logger.Buzz.Noop.LogError("The workflow '{aion.workflow.path}' does not exist.", workflowPath.Workflow);
            return new WorkflowSpec.Yuck { Path = workflowPath, Issues = ["The workflow file does not exist."] };
        }

        try
        {
            await using var fileStream = new FileStream(workflowPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var file = await JsonSerializer.DeserializeAsync<WorkflowSpecJson>(fileStream, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            });

            if (file is null)
            {
                logger.Buzz.Fail.LogError("The workflow '{aion.workflow.path}' could not be deserialized.", workflowPath.Workflow);
                return new WorkflowSpec.Yuck { Path = workflowPath, Issues = ["The workflow file is null or empty."] };
            }

            var yuck = new WorkflowSpec.Yuck { Path = workflowPath, Issues = [.. validateWorkflowFile.That(file)] };
            if (yuck.Issues.Any())
            {
                return yuck;
            }

            // note: At this point we know that the file is okay.
            var spec = new WorkflowSpec.Okay
            {
                Path = workflowPath,
                Cron = file.Cron!,
                Disabled = file.Disabled == true,
                Variables =
                    profile
                        .Variables
                        .Concat(file.Variables!.Select(x => new KeyValuePair<string, string>(x.Key, x.Value!)))
                        .LastValueWins()
                        .ToImmutableDictionary(),
                Logger =
                    // core: Switch to the profile logger if the file logger is "@profile".
                    file.Logger?.Equals("@profile", StringComparison.OrdinalIgnoreCase) == true
                        ? profile.Logger
                        // core: Use own logger, or the profile's if own one is not specified.
                        : file.Logger ?? profile.Logger,
                Steps =
                [
                    .. file.Steps!.Select((step, index) => new WorkflowSpec.Step
                    {
                        Index = index,
                        Name = step!.Name,
                        Disabled = step.Disabled == true,
                        FileName = step.FileName!,
                        Arguments = step.Arguments?.Select(x => x!).ToImmutableList() ?? [],
                        Environment = step.Environment?.ToImmutableDictionary(x => x.Key, x => x.Value!) ?? ImmutableDictionary<string, string>.Empty,
                        WorkingDirectory = step.WorkingDirectory,
                        Timeout = step.Timeout ?? System.Threading.Timeout.InfiniteTimeSpan
                    })
                ]
            };
            return spec;
        }
        catch (Exception e)
        {
            logger.Buzz.Fail.LogError(e, "An error occurred while deserializing the workflow '{aion.workflow.path}'.");
            return new WorkflowSpec.Yuck { Path = workflowPath, Issues = [$"An error occurred while deserializing the workflow file: {e.Message}"] };
        }
    }
}