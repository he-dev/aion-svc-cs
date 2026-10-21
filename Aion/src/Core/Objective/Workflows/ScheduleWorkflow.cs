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
            var workflow = await composeWorkflow.From(workflowPath, new WorkflowPatch());
            var trigger = workflow.Trigger;

            using var scope = logger.BeginActivity<ScheduleWorkflow>(("aion.workflow.trigger.key", trigger.Key.ToString()));

            // core: Compare cron expressions only if the workflow is already scheduled.
            if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
            {
                // note: This is pretty impossible, but just in case.
                var cronTrigger = trigger as ICronTrigger ?? throw new InvalidOperationException($"{nameof(AsCron)} supports only cron triggers: {trigger.Key}");

                // core: Compare the cron expressions.
                if (cronTrigger is { CronExpressionString: { } otherCron } && currentCron.Equals(otherCron))
                {
                    logger.User.LogInformation("Trigger '{aion.workflow.trigger.key}' has not changed.");
                    continue;
                }

                using var cronScope = logger.BeginScopeWith(("aion.workflow.trigger.cron", cronTrigger.CronExpressionString));
                logger.User.LogInformation("Trigger '{aion.workflow.trigger.key}' has a new schedule: {aion.workflow.trigger.cron}.");
                if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
                {
                    using var _ = logger.BeginScopeWith(("aion.workflow.trigger.next", next));
                    logger.System.LogInformation("Trigger '{aion.workflow.trigger.key}' rescheduled. Next execution at '{aion.workflow.trigger.next}'.");
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

                var nextNew = await scheduler.ScheduleJob(jobDetail, trigger);
                using var _ = logger.BeginScopeWith(("aion.workflow.trigger.next", nextNew));
                logger.System.LogInformation("Trigger '{aion.workflow.trigger.key}' scheduled. Next execution at '{aion.workflow.trigger.next}'.");
            }
        }
    }

    public async Task AsOnce(string workflowPattern, DateTimeOffset startAtUtc)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            var workflow = await composeWorkflow.From(workflowPath, new WorkflowPatch());
            var trigger = workflow.Trigger;

            if (trigger is ICronTrigger) throw new InvalidOperationException($"{nameof(AsOnce)} does not support cron triggers: {trigger.Key}.");

            using var scope = logger.BeginActivity<ScheduleWorkflow>(("aion.workflow.trigger.key", trigger.Key.ToString()));

            if (await scheduler.GetTrigger(trigger.Key) is { } current)
            {
                if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
                {
                    using var _ = logger.BeginScopeWith(("aion.workflow.trigger.next", next));
                    logger.System.LogInformation("Trigger '{aion.workflow.trigger.key}' rescheduled. Next execution at '{aion.workflow.trigger.next}'.");
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

                var nextNew = await scheduler.ScheduleJob(jobDetail, trigger);
                using var _ = logger.BeginScopeWith(("aion.workflow.trigger.next", nextNew));
                logger.System.LogInformation("Trigger '{aion.workflow.trigger.key}' scheduled. Next execution at '{aion.workflow.trigger.next}'.");
            }
        }
    }

    public async Task Cancellation(string workflowPattern)
    {
        await Task.CompletedTask;
    }

    public async Task Deletion(ITrigger trigger)
    {
        using var scope = logger.BeginActivity<ScheduleWorkflow>(("aion.workflow.trigger.key", trigger.Key.ToString()));
        var scheduler = await schedulerFactory.GetScheduler();
        try
        {
            if (await scheduler.DeleteJob(trigger.JobKey))
            {
                logger.System.LogInformation("Trigger '{aion.workflow.trigger.key}' has been deleted.");
            }
            else
            {
                logger.System.LogInformation("Trigger '{aion.workflow.trigger.key}' has not been deleted because it does not exist.");
            }
        }
        catch (Exception e)
        {
            logger.System.LogError(e, "Unable to delete trigger '{aion.workflow.trigger.key}'.");
        }
    }

    // note: This can only succeed, otherwise it throws an exception.
    public async Task<bool> At(ITrigger trigger)
    {
        using var scope = logger.BeginActivity<ScheduleWorkflow>(("aion.workflow.trigger.key", trigger.Key.ToString()));
        var scheduler = await schedulerFactory.GetScheduler();

        var jobDetail =
            JobBuilder
                .Create<WorkflowRegularJob>()
                .WithIdentity(trigger.JobKey)
                .DisallowConcurrentExecution()
                .Build();

        if (trigger is ICronTrigger cronTrigger)
        {
            // core: Compare cron expressions only if the workflow is already scheduled.
            if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
            {
                // core: Compare the cron expressions.
                if (cronTrigger is { CronExpressionString: { } otherCron } && currentCron.Equals(otherCron))
                {
                    logger.System.LogInformation("Trigger '{aion.workflow.trigger.key}' has not changed.");
                    return false;
                }

                using var cronScope = logger.BeginScopeWith(("aion.workflow.trigger.cron", cronTrigger.CronExpressionString));
                logger.System.LogInformation("Trigger '{aion.workflow.trigger.key}' has a new schedule: {aion.workflow.trigger.cron}.");
            }
            else
            {
                logger.System.LogError("Trigger '{aion.workflow.trigger.key}' not found.");
                return false;
            }
        }

        if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
        {
            using var _ = logger.BeginScopeWith(("aion.workflow.trigger.next", next));
            logger.System.LogInformation("Trigger '{aion.workflow.trigger.key}' rescheduled. Next execution at '{aion.workflow.trigger.next}'.");
            return true;
        }
        else
        {
            var nextNew = await scheduler.ScheduleJob(jobDetail, trigger);
            using var _ = logger.BeginScopeWith(("aion.workflow.trigger.next", nextNew));
            logger.System.LogInformation("Trigger '{aion.workflow.trigger.key}' scheduled. Next execution at '{aion.workflow.trigger.next}'.");
            return true;
        }
    }

    // meta: Cannot be IAsyncEnumerable because of try/catch.
    public async Task<bool> Directory()
    {
        using var activity = logger.BeginActivity<ScheduleWorkflow>
        (
            ("aion.scheduler.home", schedulerOptions.Value.Home)
        );

        try
        {
            foreach (var workflowPath in findWorkflows.Where())
            {
                if (await composeWorkflow.From(workflowPath, WorkflowPatch.Empty) is { } workflow)
                {
                    // todo: Log schedule changes.
                    var scheduleChange = await At(workflow.Trigger);
                }
            }

            activity.System.Last.LogInformation("Synchronization of '{aion.scheduler.home}' finished in {aion.activity.duration_ms}.");

            return true;
        }
        catch (Exception ex)
        {
            activity.System.Last.LogError(ex, "Synchronization of '{aion.scheduler.home}' failed.");
            return false;
        }
    }
}