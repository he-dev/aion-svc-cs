using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Interface.Jobs;
using Aion.Meta.Logging;
using Aion.Util.Buzz;
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
    public async Task<ScheduleResult> Invoke(Workflow workflow)
    {
        using var scope = logger.BeginScopeFrom(new { WorkflowPath = workflow.Path });
        var scheduler = await schedulerFactory.GetScheduler();

        if (!workflow.Enabled)
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowPath}' has been unscheduled because it is disabled.", workflow.Path);
                return new ScheduleResult(workflow.Path, SynchronizationReason.DeleteBecauseDisabled);
            }

            logger.LogInformation("Workflow '{WorkflowPath}' is not scheduled.", workflow.Path);
            return new ScheduleResult(workflow.Path, SynchronizationReason.None);
        }

        if (!workflow.Steps.Any(step => step.Enabled))
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowPath}' has been unscheduled because it is empty.", workflow.Path);
                return new ScheduleResult(workflow.Path, SynchronizationReason.DeleteBecauseEmpty);
            }

            logger.LogInformation("Workflow '{WorkflowPath}' is not scheduled.", workflow.Path);
            return new ScheduleResult(workflow.Path, SynchronizationReason.None);
        }

        var jobDetail =
            JobBuilder
                .Create<WorkflowExecutionJob>()
                .WithIdentity(workflow.Trigger.JobKey)
                .DisallowConcurrentExecution()
                .Build();

        if (workflow.Trigger is ICronTrigger trigger)
        {
            if (await scheduler.CheckExists(trigger.JobKey))
            {
                // core: Compare cron expressions only if the workflow is already scheduled.
                if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
                {
                    // core: Compare the cron expressions.
                    if (trigger is ICronTrigger { CronExpressionString: { } otherCron } && !currentCron.Equals(otherCron))
                    {
                        logger.LogDebug("Workflow '{WorkflowPath}' has a different cron expression: '{CurrentCron}' != '{OtherCron}'.", workflow.Path, currentCron, otherCron);

                        if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
                        {
                            logger.LogInformation("Workflow '{WorkflowPath}' has been rescheduled to '{Cron}'; next execution at '{Next}'.", workflow.Path, otherCron, next);
                            return new ScheduleResult(workflow.Path, SynchronizationReason.RescheduleBecauseChanged, next);
                        }
                        else
                        {
                            logger.LogError("Workflow '{WorkflowPath}' could not be rescheduled.", workflow.Path);
                        }
                    }
                }
            }
            else
            {
                var next = await scheduler.ScheduleJob(jobDetail, trigger);
                logger.LogInformation("Workflow '{WorkflowPath}' has been scheduled to '{Cron}'; next execution at '{Next}'.", workflow.Path, trigger.CronExpressionString, next);
                return new ScheduleResult(workflow.Path, SynchronizationReason.ScheduleBecauseNew, next);
            }

            return new ScheduleResult(workflow.Path, SynchronizationReason.None);
        }
        else
        {
            if (await scheduler.RescheduleJob(workflow.Trigger.Key, workflow.Trigger) is { } next)
            {
                logger.LogInformation("Workflow '{WorkflowPath}' will be executed at '{Next}'.", workflow.Path, next);
                return new ScheduleResult(workflow.Path, SynchronizationReason.RescheduleBecauseChanged, next);
            }
            else
            {
                logger.LogInformation("Workflow '{WorkflowPath}' will be executed once at '{Next}'.", workflow.Path, workflow.Trigger.GetNextFireTimeUtc());

                var nextNew = await scheduler.ScheduleJob(jobDetail, workflow.Trigger);
                return new ScheduleResult(workflow.Path, SynchronizationReason.ScheduleBecauseNew, nextNew);
            }
        }


        logger.LogInformation("No workflow synchronization strategy applies to workflow '{WorkflowPath}'.", workflow.Path);
        //return new SynchronizeWorkflowResult(workflow.Path);
        return new ScheduleResult(workflow.Path, SynchronizationReason.None);
    }
}

public record ScheduleResult(WorkflowPath WorkflowPath, SynchronizationReason Reason, DateTimeOffset? Next = null);

public enum SynchronizationReason
{
    None,
    DeleteBecauseDisabled,
    DeleteBecauseEmpty,
    RescheduleBecauseChanged,
    ScheduleBecauseNew
}