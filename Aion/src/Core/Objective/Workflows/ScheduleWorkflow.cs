using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Interface.Jobs;
using Aion.Meta.Logging;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Objective.Workflows;

public class ScheduleWorkflow
(
    ILogger<ScheduleWorkflow> logger,
    ISchedulerFactory schedulerFactory
)
{
    // https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

    // note: This can only succeed, otherwise it throws an exception.
    public async Task<bool> Invoke(Workflow workflow, IProgress<ScheduleChange> progress)
    {
        using var scope = logger.BeginScopeFrom(new { WorkflowPath = workflow.Path });
        var scheduler = await schedulerFactory.GetScheduler();

        if (workflow.Disabled)
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowPath}' has been unscheduled because it is disabled.", workflow.Path);
                progress.Report(new ScheduleChange.DeleteBecauseDisabled(workflow.Path));
                return true;
            }

            logger.LogInformation("Workflow '{WorkflowPath}' is not scheduled.", workflow.Path);
            return false;
        }

        if (workflow.Steps.All(step => step.Disabled))
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowPath}' has been unscheduled because it is empty.", workflow.Path);
                progress.Report(new ScheduleChange.DeleteBecauseEmpty(workflow.Path));
                return true;
            }

            logger.LogInformation("Workflow '{WorkflowPath}' is not scheduled.", workflow.Path);
            return false;
        }

        var jobDetail =
            JobBuilder
                .Create<WorkflowExecutionJob>()
                .WithIdentity(workflow.Trigger.JobKey)
                .DisallowConcurrentExecution()
                .Build();

        if (workflow.Trigger is ICronTrigger trigger)
        {
            // core: Compare cron expressions only if the workflow is already scheduled.
            if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
            {
                // core: Compare the cron expressions.
                if (trigger is { CronExpressionString: { } otherCron } && currentCron.Equals(otherCron))
                {
                    logger.LogInformation("Workflow '{WorkflowPath}' has the same cron expression: '{Cron}'.", workflow.Path, currentCron);
                    return false;
                }

                logger.LogInformation("Workflow '{WorkflowPath}' schedule has changed to: '{CurrentCron}'.", workflow.Path, currentCron);
            }
            else
            {
                logger.LogError("Workflow trigger for '{WorkflowPath}' could not be retrieved.", workflow.Path);
                return false;
            }
        }

        if (await scheduler.RescheduleJob(workflow.Trigger.Key, workflow.Trigger) is { } next)
        {
            logger.LogInformation("Workflow '{WorkflowPath}' will be executed at '{Next}'.", workflow.Path, next);
            progress.Report(new ScheduleChange.RescheduleBecauseChanged(workflow.Path, next));
            return true;
        }

        var nextNew = await scheduler.ScheduleJob(jobDetail, workflow.Trigger);
        logger.LogInformation("Workflow '{WorkflowPath}' will be executed at '{Next}'.", workflow.Path, workflow.Trigger.NextFireTimeUtc);
        progress.Report(new ScheduleChange.ScheduleBecauseNew(workflow.Path, nextNew));
        return true;
    }
}

public abstract record ScheduleChange(WorkflowPath WorkflowPath, DateTimeOffset? Next = null)
{
    public string Reason => GetType().Name;

    public record DeleteBecauseDisabled(WorkflowPath WorkflowPath) : ScheduleChange(WorkflowPath);

    public record DeleteBecauseEmpty(WorkflowPath WorkflowPath) : ScheduleChange(WorkflowPath);

    public record ScheduleBecauseNew(WorkflowPath WorkflowPath, DateTimeOffset? Next) : ScheduleChange(WorkflowPath, Next);

    public record RescheduleBecauseChanged(WorkflowPath WorkflowPath, DateTimeOffset? Next) : ScheduleChange(WorkflowPath, Next);
}

public class ScheduleWorkflowProgress : List<ScheduleChange>, IProgress<ScheduleChange>
{
    public void Report(ScheduleChange value) => Add(value);
}