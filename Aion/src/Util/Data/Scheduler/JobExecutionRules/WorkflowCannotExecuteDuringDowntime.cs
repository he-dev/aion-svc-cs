using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util.Buzz;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Util.Data.Scheduler.JobExecutionRules;

public class WorkflowCannotExecuteDuringDowntime
(
    ILogger<WorkflowCannotExecuteDuringDowntime> logger,
    FindWorkflows findWorkflows
) : ITriggerListener
{
    public string Name => nameof(WorkflowCannotExecuteDuringDowntime);

    public async Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var workflowPath = trigger.JobDataMap.GetString(JobDataKeys.WorkflowPath)!;

        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflowPath });

        try
        {
            // core: Get the triggered workflow's path and check its downtime.
            var workflowMatch = findWorkflows.Where(workflowPath).Single();
            if (await WorkflowDowntime.FromFile(workflowPath) is { } workflowDowntime)
            {
                if (workflowDowntime.Status == WorkflowDowntimeStatus.Expired)
                {
                    logger.LogWarning("Workflow downtime has expired on {ExpiresOn} and will be deleted.", workflowDowntime.EndsOnUtc.ToLocalTime());
                    await workflowDowntime.EndsNow();
                }

                logger.LogDebug("Workflow '{WorkflowName}' is currently {Status}.", workflowPath, workflowDowntime.Status);
                return workflowDowntime.Status == WorkflowDowntimeStatus.Ongoing;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow downtime could not be checked.");

            try
            {
                if (ex is WorkflowDowntimeIssue issue)
                {
                    File.Delete(issue.Path);
                }
            }
            catch (Exception inner)
            {
                logger.LogError(inner, "Workflow downtime could not be cleaned up.");
            }

            // core: Use the fail-close principle. If not sure whether it's open, then it's closed.
            return true;
        }

        logger.LogDebug("Workflow '{WorkflowName}' does not have an ongoing downtime.", workflowPath);
        return false;
    }

    public Task TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var profileName = trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var workflowName = trigger.JobDataMap.GetString(JobDataKeys.WorkflowPath)!;

        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        logger.LogDebug("Workflow trigger '{TriggerName}' has fired.", trigger.Key.Name);
        return Task.CompletedTask;
    }

    public Task TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = default)
    {
        var profileName = trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var workflowName = trigger.JobDataMap.GetString(JobDataKeys.WorkflowPath)!;

        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        logger.LogWarning("Workflow trigger '{TriggerName}' has misfired.", trigger.Key.Name);
        return Task.CompletedTask;
    }

    public Task TriggerComplete(ITrigger trigger, IJobExecutionContext context, SchedulerInstruction triggerInstructionCode, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}