using System;
using System.Collections.Generic;
using Aion.Util.Data;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Util.Buzz;

public class ValidateWorkflowSpec(IOptions<SchedulerOptions> schedulerOptions)
{
    public IEnumerable<string> That(WorkflowSpec context)
    {
        if (string.IsNullOrWhiteSpace(context.Cron))
        {
            yield return $"Cannot use [{nameof(WorkflowSpec)}.{nameof(WorkflowSpec.Cron)}] because it is null or empty.";
        }
        else
        {
            var cronErrorMessage = (string?)null;
            try
            {
                _ = CronExpression.Parse(context.Cron);
            }
            catch (Exception e)
            {
                cronErrorMessage = e.Message;
            }

            if (cronErrorMessage is not null)
            {
                yield return $"Cannot use [{nameof(WorkflowSpec)}.{nameof(WorkflowSpec.Cron)}] because it is not a valid cron expression: {cronErrorMessage}";
            }
        }


        foreach (var (key, value) in context.Variables)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield return $"Cannot use [{nameof(WorkflowSpec)}.{nameof(WorkflowSpec.Variables)}][{key}] because its value is null or empty.";
            }
        }

        if (context.Steps is not { Length: > 0 })
        {
            yield return $"Cannot use [{nameof(WorkflowSpec)}.{nameof(WorkflowSpec.Steps)}] because it is null or empty.";
        }

        if (context.Logger is not null)
        {
            var loggersPath = System.IO.Path.Join(schedulerOptions.Value.Home, "loggers", $"{context.Logger}.json");
            if (!System.IO.File.Exists(loggersPath))
            {
                yield return $"Cannot use [{nameof(WorkflowSpec)}.{nameof(WorkflowSpec.Logger)}]'s '{context.Logger}' because the file '{loggersPath}' it refers to, does not exist.";
            }
        }
    }
}