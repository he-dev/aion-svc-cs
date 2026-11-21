using Aion.Data.Configs;
using Aion.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiretap.Core;

namespace Aion.Util;

public class ValidateWorkflowConfigDefault
(
    ILogger<ValidateWorkflowConfigDefault> logger,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    public bool That(WorkflowConfigDefault workflowConfigDefault)
    {
        var isValid = true;
        if (workflowConfigDefault.Variables is not null)
        {
            foreach (var (key, value) in workflowConfigDefault.Variables)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    logger.Baseline.LogError("Workflow profile issue: {workflow.profile.issue}.", $"Variable '{key}' is null or empty.");
                    isValid = false;
                }
            }
        }

        if (workflowConfigDefault.Logger is not null)
        {
            var loggersPath = System.IO.Path.Join(schedulerOptions.Value.Home, "loggers", $"{workflowConfigDefault.Logger}.json");

            if (!System.IO.File.Exists(loggersPath))
            {
                logger.Baseline.LogError("Workflow profile issue: {workflow.profile.issue}.", $"Logger '{workflowConfigDefault.Logger}' does not exist.");
                isValid = false;
            }
        }

        return isValid;
    }
}