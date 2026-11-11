using System;
using System.Threading.Tasks;
using Aion.Core.Hello.Jobs;
using Aion.Data;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Quartz;
using Wiretap.Core;

namespace Aion.Core.Magic.Workflows;

public class ScheduleWorkflow
(
    ILogger<ScheduleWorkflow> logger,
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
            using var about = logger.Baseline.BeginScopeAbout<ScheduleWorkflow>
            ([
                ("@workflow.name", workflowPath),
            ]);
            about.Zero.Internal.LogDebug(null);

            if (await composeWorkflow.From(workflowPath) is not { } workflow)
            {
                about.Last.LogError("Skips workflow '{@workflow.name}' because it did not load.", workflowPath);
                continue;
            }

            if (workflow.Trigger is not ICronTrigger cronTrigger)
            {
                about.Last.LogError("Skips workflow '{@workflow.name}' because its trigger is not cron.", workflowPath);
                continue;
            }

            about.Tick.LogDebug("Trigger '{workflow.trigger.key}': {workflow.trigger.cron}", cronTrigger.Key, cronTrigger.CronExpressionString);

            // core: Compare cron expressions only if the workflow is already scheduled.
            if (await scheduler.GetTrigger(cronTrigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
            {
                // core: Compare the cron expressions.
                if (cronTrigger is { CronExpressionString: { } otherCron } && currentCron.Equals(otherCron))
                {
                    about.Last.LogInformation("Workflow '{@workflow.name}' has not changed.");
                    continue;
                }

                if (await scheduler.RescheduleJob(cronTrigger.Key, cronTrigger) is { } next)
                {
                    about.Last.LogInformation
                    (
                        "Reschedules workflow '{@workflow.name}' to '{workflow.trigger.cron}'. Next execution at '{workflow.trigger.next}'.",
                        workflowPath,
                        cronTrigger.CronExpressionString,
                        next
                    );
                }
                else
                {
                    about.Last.LogError("Cannot reschedule '{@workflow.name}'.");
                }
            }
            else
            {
                var jobDetail =
                    JobBuilder
                        .Create<WorkflowRegularJob>()
                        .WithIdentity(cronTrigger.JobKey)
                        .DisallowConcurrentExecution()
                        .Build();

                var next = await scheduler.ScheduleJob(jobDetail, cronTrigger);
                about.Last.LogInformation
                (
                    "Schedules workflow '{@workflow.name}' to '{workflow.trigger.cron}'. Next execution at '{workflow.trigger.next}'.",
                    workflowPath,
                    cronTrigger.CronExpressionString,
                    next
                );
            }
        }
    }

    public async Task AsOnce(string workflowPattern, WorkflowOverride workflowOverride)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            using var about = logger.Baseline.BeginScopeAbout<ScheduleWorkflow>
            ([
                ("@workflow.name", workflowPath),
            ]);
            about.Zero.Internal.LogDebug(null);

            if (await composeWorkflow.From(workflowPath, workflowOverride) is not { } workflow)
            {
                logger.Baseline.LogError("Skips workflow '{@workflow.name}' because it did not load.");
                continue;
            }

            if (workflow.Trigger is ICronTrigger)
            {
                about.Last.LogError("Skips workflow {@workflow.name} because its trigger is cron.");
                continue;
            }

            if (await scheduler.GetTrigger(workflow.Trigger.Key) is not null)
            {
                if (await scheduler.RescheduleJob(workflow.Trigger.Key, workflow.Trigger) is { } next)
                {
                    about.Last.LogInformation
                    (
                        "Reschedules {@workflow.name}. Next execution at '{workflow.trigger.next}'.",
                        workflowPath,
                        next
                    );
                }
                else
                {
                    about.Last.LogError("Cannot reschedule '{@workflow.name}'.");
                }
            }
            else
            {
                var jobDetail =
                    JobBuilder
                        .Create<WorkflowCustomJob>()
                        .WithIdentity(workflow.Trigger.JobKey)
                        .UsingJobData(JobDataKeys.WorkflowPath, workflowPath)
                        .UsingJobData(JobDataKeys.WorkflowMod, workflowOverride)
                        .DisallowConcurrentExecution()
                        .Build();

                var nextNew = await scheduler.ScheduleJob(jobDetail, workflow.Trigger);
                about.Last.LogInformation(
                    "Schedules workflows '{@workflow.name}'. Next execution at '{workflow.trigger.next}'.",
                    workflowPath,
                    nextNew
                );
            }
        }
    }

    public async Task Deletion(ITrigger trigger)
    {
        using var scope = logger.Baseline.BeginScopeAbout<ScheduleWorkflow>
        ([
            ("workflow.trigger.key", trigger.Key)
        ]);
        scope.Zero.Internal.LogDebug(null);

        try
        {
            var scheduler = await schedulerFactory.GetScheduler();
            if (await scheduler.DeleteJob(trigger.JobKey))
            {
                scope.Last.LogInformation("Deletes trigger '{workflow.trigger.key}'.");
            }
            else
            {
                scope.Last.LogInformation("Does not delete trigger '{workflow.trigger.key}' because it does not exist.");
            }
        }
        catch (Exception e)
        {
            scope.Last.LogError(e, "Fails to delete trigger '{workflow.trigger.key}'.");
        }
    }
}