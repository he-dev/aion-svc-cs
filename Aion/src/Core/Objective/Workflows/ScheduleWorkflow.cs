using System;
using System.Threading.Tasks;
using Aion.Core.Interface.Jobs;
using Aion.Meta.Logging.Wiretap;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Objective.Workflows;

public class ScheduleWorkflow
(
    ILogger<ScheduleWorkflow> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    ISchedulerFactory schedulerFactory,
    FindWorkflows findWorkflows,
    ComposeWorkflow composeWorkflow
)
{
    // https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

    public async Task AsCron(string? workflowPattern = null)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            var workflow = await composeWorkflow.From(workflowPath);
            if (workflow is null)
            {
                logger.Domain.LogError("Skipping workflow '{@workflow.name}' because it did not load.", workflowPath);
                continue;
            }

            var trigger = workflow.Trigger;

            using var scope = logger.BeginScopeAbout<ScheduleWorkflow>([("workflow.trigger.key", trigger.Key.ToString())]);

            // note: This is pretty impossible, but just in case.
            var cronTrigger = trigger as ICronTrigger ?? throw new InvalidOperationException($"{nameof(AsCron)} supports only cron triggers: {trigger.Key}");

            // core: Compare cron expressions only if the workflow is already scheduled.
            if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
            {
                // core: Compare the cron expressions.
                if (cronTrigger is { CronExpressionString: { } otherCron } && currentCron.Equals(otherCron))
                {
                    logger.Domain.LogInformation("Workflow '{@workflow.name}' has not changed.", workflowPath);
                    continue;
                }

                using var cronScope = logger.BeginScopeWith(("aion.workflow.trigger.cron", cronTrigger.CronExpressionString));
                logger.Domain.LogInformation("Workflow '{@workflow.name}' has a new schedule: '{workflow.trigger.cron}'.", workflowPath, cronTrigger.CronExpressionString);
                if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
                {
                    logger.Domain.LogInformation("Workflow '{@workflow.name}' has been rescheduled to '{workflow.trigger.cron}'. Next execution at '{workflow.trigger.next}'.", workflowPath, cronTrigger.CronExpressionString, next);
                }
            }
            else
            {
                var jobDetail =
                    JobBuilder
                        .Create<WorkflowRegularJob>()
                        .WithIdentity(trigger.JobKey)
                        .DisallowConcurrentExecution()
                        .Build();

                var next = await scheduler.ScheduleJob(jobDetail, trigger);
                using var _ = logger.BeginScopeWith(("aion.workflow.trigger.next", next));
                logger.System.LogInformation("Workflow '{@workflow.name}' scheduled to '{workflow.trigger.cron}'. Next execution at '{workflow.trigger.next}'.", workflowPath, cronTrigger.CronExpressionString, next);
            }
        }
    }

    public async Task AsOnce(string workflowPattern, WorkflowMod workflowMod)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            var workflow = await composeWorkflow.From(workflowPath, workflowMod);
            var trigger = workflow.Trigger;

            if (trigger is ICronTrigger) throw new InvalidOperationException($"{nameof(AsOnce)} does not support cron triggers: {trigger.Key}.");

            using var scope = logger.BeginScopeAbout<ScheduleWorkflow>([("workflow.trigger.key", trigger.Key.ToString())]);

            if (await scheduler.GetTrigger(trigger.Key) is { } current)
            {
                if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
                {
                    using var _ = logger.BeginScopeWith(("workflow.trigger.next", next));
                    logger.System.LogInformation("Trigger '{workflow.trigger.key}' rescheduled. Next execution at '{workflow.trigger.next}'.");
                }
            }
            else
            {
                var jobDetail =
                    JobBuilder
                        .Create<WorkflowCustomJob>()
                        .WithIdentity(trigger.JobKey)
                        .UsingJobData(JobDataKeys.WorkflowPath, workflowPath)
                        .UsingJobData(JobDataKeys.WorkflowMod, workflowMod)
                        .DisallowConcurrentExecution()
                        .Build();

                var nextNew = await scheduler.ScheduleJob(jobDetail, trigger);
                using var _ = logger.BeginScopeWith(("workflow.trigger.next", nextNew));
                logger.System.LogInformation("Trigger '{workflow.trigger.key}' scheduled. Next execution at '{workflow.trigger.next}'.");
            }
        }
    }

    public async Task Cancellation(string workflowPattern)
    {
        await Task.CompletedTask;
    }

    public async Task Deletion(ITrigger trigger)
    {
        using var scope = logger.BeginScopeAbout<ScheduleWorkflow>([("aion.workflow.trigger.key", trigger.Key.ToString())]);
        var scheduler = await schedulerFactory.GetScheduler();
        try
        {
            if (await scheduler.DeleteJob(trigger.JobKey))
            {
                logger.System.LogInformation("Trigger '{workflow.trigger.key}' has been deleted.");
            }
            else
            {
                logger.System.LogInformation("Trigger '{workflow.trigger.key}' has not been deleted because it does not exist.");
            }
        }
        catch (Exception e)
        {
            logger.System.LogError(e, "Unable to delete trigger '{workflow.trigger.key}'.");
        }
    }
}