using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.Util.Data;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Util.Buzz;

public class ValidateWorkflowFile(IOptions<SchedulerOptions> schedulerOptions)
{
    public IEnumerable<string> That(WorkflowSpecJson workflow)
    {
        if (string.IsNullOrWhiteSpace(workflow.Cron))
        {
            yield return $"Cannot use [{nameof(WorkflowSpecJson)}.{nameof(WorkflowSpecJson.Cron)}] because it is null or empty.";
        }
        else
        {
            var cronErrorMessage = (string?)null;
            try
            {
                _ = CronExpression.Parse(workflow.Cron);
            }
            catch (Exception e)
            {
                cronErrorMessage = e.Message;
            }

            if (cronErrorMessage is not null)
            {
                yield return $"Cannot use [{nameof(WorkflowSpecJson)}.{nameof(WorkflowSpecJson.Cron)}] because it is not a valid cron expression: {cronErrorMessage}";
            }
        }

        if (workflow.Variables is { } variables)
        {
            foreach (var (key, value) in variables)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    yield return $"Cannot use [{nameof(WorkflowSpecJson)}.{nameof(WorkflowSpecJson.Variables)}][{key}] because its value is null or empty.";
                }
            }
        }

        if (workflow.Logger is not null)
        {
            if (!workflow.Logger.Equals("@profile", StringComparison.OrdinalIgnoreCase))
            {
                var loggersPath = System.IO.Path.Join(schedulerOptions.Value.Home, "loggers", $"{workflow.Logger}.json");
                if (!System.IO.File.Exists(loggersPath))
                {
                    yield return $"Cannot use [{nameof(WorkflowSpecJson)}.{nameof(WorkflowSpecJson.Logger)}]'s '{workflow.Logger}' because the file '{loggersPath}' it refers to, does not exist.";
                }
            }
        }

        if (workflow.Steps is null)
        {
            yield return $"Cannot use [{nameof(WorkflowSpecJson)}.{nameof(WorkflowSpecJson.Steps)}] because it is null.";
        }
        else
        {
            if (workflow.Steps.Length == 0)
            {
                yield return $"Cannot use [{nameof(WorkflowSpecJson)}.{nameof(WorkflowSpecJson.Steps)}] because it is null or empty.";
            }
            else
            {
                if (workflow.Steps.Any(step => step is null))
                {
                    yield return $"Cannot use [{nameof(WorkflowSpecJson)}.{nameof(WorkflowSpecJson.Steps)}] because one or more steps are null.";
                }
                else
                {
                    foreach (var (step, index) in workflow.Steps.Select((x, i) => (x, i)))
                    {
                        // note: At this point steps are not null. C# just cannot figure that out.
                        foreach (var issue in That(step!, index))
                        {
                            yield return issue;
                        }
                    }
                }
            }
        }
    }

    private IEnumerable<string> That(WorkflowSpecJson.Step step, int index)
    {
        if (step.Name is not null && string.IsNullOrWhiteSpace(step.Name))
        {
            yield return $"Cannot use [{nameof(WorkflowSpecJson.Step)}.{nameof(step.Name)}] at index {index} because it is empty.";
        }

        if (string.IsNullOrWhiteSpace(step.FileName))
        {
            yield return $"Cannot use [{nameof(WorkflowSpecJson.Step)}.{nameof(step.FileName)}] at index {index} because it is empty.";
        }

        if(step.Arguments is not null)
        {
            foreach (var argument in step.Arguments)
            {
                if (string.IsNullOrWhiteSpace(argument))
                {
                    yield return $"Cannot use [{nameof(WorkflowSpecJson.Step)}.{nameof(step.Arguments)}] at index {index} because it contains an empty or null argument.";
                }
            }
        }

        if (step.WorkingDirectory is not null && !Directory.Exists(step.WorkingDirectory))
        {
            yield return $"Cannot use [{nameof(WorkflowSpecJson.Step)}.{nameof(step.WorkingDirectory)}] at index {index} that refers to '{step.WorkingDirectory}' because it does not exist.";
        }

        if (step.Timeout is not null && step.Timeout < TimeSpan.Zero)
        {
            yield return $"Cannot use [{nameof(WorkflowSpecJson.Step)}.{nameof(step.Timeout)}] at index {index} because it is less than zero.";
        }
    }
}