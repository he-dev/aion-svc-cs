using System.Collections.Generic;
using Aion.Util.Data;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Options;

namespace Aion.Util.Buzz;

public class ValidateWorkflowProfile(IOptions<SchedulerOptions> schedulerOptions)
{
    public IEnumerable<string> That(WorkflowProfileJson profile)
    {
        if (profile.Variables is not null)
        {
            foreach (var (key, value) in profile.Variables)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    yield return $"Cannot use [{nameof(WorkflowProfile)}.{nameof(WorkflowProfileJson.Variables)}][{key}] because its value is null or empty.";
                }
            }
        }

        if (profile.Logger is not null)
        {
            var loggersPath = System.IO.Path.Join(schedulerOptions.Value.Home, "loggers", $"{profile.Logger}.json");

            if (!System.IO.File.Exists(loggersPath))
            {
                yield return $"Cannot use [{nameof(WorkflowProfile)}.{nameof(WorkflowProfileJson.Logger)}]'s '{profile.Logger}' because the file '{loggersPath}' it refers to, does not exist.";
            }
        }
    }
}