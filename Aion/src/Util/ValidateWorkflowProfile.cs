using System.Collections.Generic;
using Aion.Data;
using Aion.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiretap.Core;

namespace Aion.Util;

public class ValidateWorkflowProfile
(
    ILogger<ValidateWorkflowProfile> logger,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    public bool That(WorkflowProfileJson profile)
    {
        var isValid = true;
        if (profile.Variables is not null)
        {
            foreach (var (key, value) in profile.Variables)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    logger.Baseline.LogError("Workflow profile issue: {workflow.profile.issue}.", $"Variable '{key}' is null or empty.");
                    isValid = false;
                }
            }
        }

        if (profile.Logger is not null)
        {
            var loggersPath = System.IO.Path.Join(schedulerOptions.Value.Home, "loggers", $"{profile.Logger}.json");

            if (!System.IO.File.Exists(loggersPath))
            {
                logger.Baseline.LogError("Workflow profile issue: {workflow.profile.issue}.", $"Logger '{profile.Logger}' does not exist.");
                isValid = false;
            }
        }

        return isValid;
    }
}