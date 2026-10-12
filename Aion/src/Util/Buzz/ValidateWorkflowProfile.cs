using System.Collections.Generic;
using Aion.Util.Data;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Options;

namespace Aion.Util.Buzz;

public class ValidateWorkflowProfile(IOptions<SchedulerOptions> schedulerOptions)
{


    public IEnumerable<string> That(WorkflowProfile context)
    {
        foreach (var (key, value) in context.Variables)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield return $"Cannot use [{nameof(WorkflowProfile)}.{nameof(WorkflowProfile.Variables)}][{key}] because its value is null or empty.";
            }
        }

        if (context.Logger is not null)
        {
            var loggersPath = System.IO.Path.Join(schedulerOptions.Value.Home, "loggers", $"{context.Logger}.json");

            if (!System.IO.File.Exists(loggersPath))
            {
                yield return $"Cannot use [{nameof(WorkflowProfile)}.{nameof(WorkflowProfile.Logger)}]'s '{context.Logger}' because the file '{loggersPath}' it refers to, does not exist.";
            }
        }
    }
}