using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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

    // note: This can only succeed, otherwise it throws an exception.
    public async Task<ScheduleChange> At<T>(ITrigger trigger) where T : Workflow
    {
        using var scope = logger.BeginScopeWith(("aion.workflow.trigger.key", trigger.Key.ToString()));
        var scheduler = await schedulerFactory.GetScheduler();

        if (typeof(T) == typeof(Workflow.Yuck))
        {
            if (await scheduler.DeleteJob(trigger.JobKey))
            {
                logger.Buzz.LogDebug("Trigger '{aion.workflow.trigger.key}' has been deleted because it is yuck.");
                return new ScheduleChange.DeleteBecauseIssues();
            }

            return new ScheduleChange.NoopBecauseNotScheduled();
        }

        var jobDetail =
            JobBuilder
                .Create<WorkflowExecutionJob>()
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
                    logger.Buzz.LogDebug("Trigger '{aion.workflow.trigger.key}' has not changed.");
                    return new ScheduleChange.NoopBecauseNoChanges();
                }

                using var cronScope = logger.BeginScopeWith(("aion.workflow.trigger.cron", cronTrigger.CronExpressionString));
                logger.Buzz.LogDebug("Trigger '{aion.workflow.trigger.key}' has a new schedule: {aion.workflow.trigger.cron}.");
            }
            else
            {
                logger.Buzz.LogError("Trigger '{aion.workflow.trigger.key}' not found.");
                return new ScheduleChange.NoopBecauseTriggerNotFound();
            }
        }

        if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
        {
            using var _ = logger.BeginScopeWith(("aion.workflow.trigger.next", next));
            logger.Buzz.LogDebug("Trigger '{aion.workflow.trigger.key}' rescheduled. Next execution at '{aion.workflow.trigger.next}'.");
            return new ScheduleChange.RescheduleBecauseChanged(next);
        }
        else
        {
            var nextNew = await scheduler.ScheduleJob(jobDetail, trigger);
            using var _ = logger.BeginScopeWith(("aion.workflow.trigger.next", nextNew));
            logger.Buzz.LogDebug("Trigger '{aion.workflow.trigger.key}' scheduled. Next execution at '{aion.workflow.trigger.next}'.");
            return new ScheduleChange.ScheduleBecauseNew(nextNew);
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
                if (await composeWorkflow.From(workflowPath, WorkflowPatch.Empty) is Workflow.Okay workflow)
                {
                    // todo: Log schedule changes.
                    var scheduleChange = await At<Workflow.Okay>(workflow.Trigger);
                }
            }

            activity.Buzz.Last.LogInformation("Synchronization of '{aion.scheduler.home}' finished in {aion.activity.duration_ms}.");

            return true;
        }
        catch (Exception ex)
        {
            activity.Buzz.Last.LogError(ex, "Synchronization of '{aion.scheduler.home}' failed.");
            return false;
        }
    }
}

public abstract record ScheduleChange(DateTimeOffset? Next = null)
{
    public string Reason => GetType().Name;

    public record NoopBecauseNotScheduled : ScheduleChange;

    public record NoopBecauseNoChanges : ScheduleChange;

    public record NoopBecauseTriggerNotFound : ScheduleChange;

    public record DeleteBecauseIssues : ScheduleChange;

    public record DeleteBecauseDisabled : ScheduleChange;

    public record DeleteBecauseEmpty : ScheduleChange;

    public record ScheduleBecauseNew(DateTimeOffset? Next) : ScheduleChange(Next);

    public record RescheduleBecauseChanged(DateTimeOffset? Next) : ScheduleChange(Next);
}

public class SynchronizeWorkflowProgress : List<ScheduleChange>, IProgress<ScheduleChange>
{
    public void Report(ScheduleChange value) => Add(value);
}