using System;
using System.IO;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Data.JobInputs;
using Aion.Data.Scheduler;
using Aion.Data.Specs;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Wiretap.Core;

namespace Aion.Core.Magic.Workflows;

public class ScheduleWorkflow
(
    ILogger<ScheduleWorkflow> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    ISchedulerFactory schedulerFactory,
    FindWorkflows findWorkflows,
    LoadWorkflow loadWorkflow
)
{
    // https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

    public async Task Polling<TJob>() where TJob : IJob
    {
        var scheduler = await schedulerFactory.GetScheduler();
        var schedulerHome = schedulerOptions.Value.Home;

        try
        {
            var trigger =
                TriggerBuilder
                    .Create<TJob>()
                    .WithIdentity(group: "job://polling", name: Path.GetDirectoryName(schedulerHome)!)
                    .WithCronSchedule(schedulerOptions.Value.Polling.Cron)
                    .StartAt(DateTimeOffset.UtcNow + schedulerOptions.Value.StartDelay)
                    .Build();

            var next = await scheduler.ScheduleJob<TJob>(trigger);

            logger.Baseline.LogInformation("Workflow polling starts. Next run at '{workflow.polling.next}'.", next);
        }
        catch (Exception ex)
        {
            logger.Baseline.LogError(ex, "Workflow polling fails.");
            throw new JobExecutionException(ex);
        }
    }

    public async Task AsCron<TJob>(WorkflowPattern workflowPattern) where TJob : IJob<WorkflowRegularInput>
    {
        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            if (await loadWorkflow.Spec(workflowPath) is { } workflow)
            {
                await AsCron<TJob>(workflow);
            }
            else
            {
                logger.Baseline.LogError("Skips workflow '{@workflow.name}' because it does not load.", workflowPath);
            }
        }
    }

    private async Task AsCron<TJob>(WorkflowSpec workflowSpec) where TJob : IJob<WorkflowRegularInput>
    {
        var scheduler = await schedulerFactory.GetScheduler();

        using var about = logger.Baseline.BeginScopeAbout<ScheduleWorkflow>
        ([
            ("workflow.name", workflowSpec.Name),
        ]);
        about.Zero.Internal.LogDebug(null);

        var cronTrigger =
            (ICronTrigger)TriggerBuilder
                .Create<TJob>()
                .WithIdentity(TriggerKey(WorkflowMode.Job, workflowSpec.Name))
                .UsingInput(new WorkflowRegularInput
                {
                    WorkflowName = workflowSpec.Name,
                    WorkflowMode = WorkflowMode.Job
                })
                .WithCronSchedule(workflowSpec.Cron)
                .Build();

        about.Tick.LogDebug("Trigger '{workflow.trigger.key}': {workflow.trigger.cron}", cronTrigger.Key, cronTrigger.CronExpressionString);

        // core: Compare cron expressions only if the workflow is already scheduled.
        if (await scheduler.GetTrigger(cronTrigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
        {
            // core: Compare both cron expressions.
            if (cronTrigger is { CronExpressionString: { } otherCron } && currentCron.Equals(otherCron, StringComparison.InvariantCultureIgnoreCase))
            {
                about.Last.LogInformation("Workflow schedule for '{workflow.name}' has not changed: '{workflow.trigger.cron}'.", workflowSpec.Name, cronTrigger.CronExpressionString);
            }
            else
            {
                if (await scheduler.RescheduleJob(cronTrigger.Key, cronTrigger) is { } next)
                {
                    about.Last.LogInformation
                    (
                        "Reschedules workflow '{workflow.name}' to '{workflow.trigger.cron}'. Next execution at '{workflow.trigger.next}'.",
                        workflowSpec.Name,
                        cronTrigger.CronExpressionString,
                        next
                    );
                }
                else
                {
                    about.Last.LogError("Cannot reschedule '{workflow.name}'.", workflowSpec.Name);
                }
            }
        }
        else
        {
            var next = await scheduler.ScheduleJob<TJob>(cronTrigger, job => job.DisallowConcurrentExecution());
            about.Last.LogInformation
            (
                "Schedules workflow '{workflow.name}' to '{workflow.trigger.cron}'. Next execution at '{workflow.trigger.next}'.",
                workflowSpec.Name,
                cronTrigger.CronExpressionString,
                next
            );
        }
    }

    public async Task AsOnce<TJob>(WorkflowPattern workflowPattern, WorkflowOverride workflowOverride) where TJob : IJob<WorkflowCustomInput>
    {
        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            var trigger =
                TriggerBuilder
                    .Create<TJob>()
                    .WithIdentity(TriggerKey(WorkflowMode.Job, workflowPath))
                    .UsingInput(new WorkflowCustomInput
                    {
                        WorkflowPath = workflowPath,
                        WorkflowMode = WorkflowMode.Job,
                        WorkflowSteps = workflowOverride.Steps
                    })
                    .StartAt(workflowOverride.StartAt)
                    .WithSimpleSchedule(x => x.WithRepeatCount(0))
                    .Build();

            if (await loadWorkflow.Spec(workflowPath) is { } workflow)
            {
                await AsOnce<TJob>(workflow, trigger);
            }
            else
            {
                logger.Snap<ScheduleWorkflow>().Baseline.LogError("Skips workflow '{workflow.name}' because it does not load.", workflowPath.Name);
            }
        }
    }

    private async Task AsOnce<TJob>(WorkflowSpec workflowSpec, ITrigger trigger) where TJob : IJob<WorkflowCustomInput>
    {
        using var about = logger.Baseline.BeginScopeAbout<ScheduleWorkflow>
        ([
            ("workflow.name", workflowSpec.Name),
        ]);
        about.Zero.Internal.LogDebug(null);

        var scheduler = await schedulerFactory.GetScheduler();
        if (await scheduler.GetTrigger(trigger.Key) is not null)
        {
            if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
            {
                about.Last.LogInformation("Reschedules {workflow.name}. Next execution at '{workflow.trigger.next}'.", workflowSpec.Name, next);
            }
            else
            {
                about.Last.LogError("Cannot reschedule '{workflow.name}'.");
            }
        }
        else
        {
            var next = await scheduler.ScheduleJob<TJob>(trigger, job => job.DisallowConcurrentExecution());
            about.Last.LogInformation("Schedules workflows '{workflow.name}'. Next execution at '{workflow.trigger.next}'.", workflowSpec.Name, next);
        }
    }

    public async Task Deletion(ITrigger trigger)
    {
        using var scope = logger.Internal.BeginScopeAbout<ScheduleWorkflow>
        ([
            ("workflow.trigger.key", trigger.Key)
        ]);
        scope.Zero.LogDebug(null);

        try
        {
            var scheduler = await schedulerFactory.GetScheduler();
            if (await scheduler.DeleteJob(trigger.JobKey))
            {
                scope.Last.LogInformation("Deletes trigger '{workflow.trigger.key}'.");
            }
            else
            {
                scope.Last.LogInformation("Trigger '{workflow.trigger.key}' does not exist.");
            }
        }
        catch (Exception e)
        {
            scope.Last.LogError(e, "Fails to delete trigger '{workflow.trigger.key}'.");
        }
    }

    public static TriggerKey TriggerKey(WorkflowMode mode, string workflowName)
    {
        return new TriggerKey
        (
            group: $"{mode}://workflows".ToLowerInvariant(),
            name: workflowName.ToLowerInvariant()
        );
    }
}