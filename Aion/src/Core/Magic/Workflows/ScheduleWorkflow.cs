using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Core.Hello.Jobs;
using Aion.Data;
using Aion.Data.Specs;
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
    LoadWorkflowSpec loadWorkflowSpec
)
{
    // https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

    public async Task AsCron(WorkflowPattern workflowPattern)
    {
        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            if (await loadWorkflowSpec.FromFile(workflowPath) is { } workflow)
            {
                await AsCron(workflow);
            }
            else
            {
                logger
                    .Baseline
                    .LogError("Skips workflow '{@workflow.name}' because it does not load.", workflowPath);
            }
        }
    }

    private async Task AsCron(WorkflowSpec workflowSpec)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        using var about = logger.Baseline.BeginScopeAbout<ScheduleWorkflow>
        ([
            ("@workflow.name", workflowSpec.Path),
        ]);
        about.Zero.Internal.LogDebug(null);

        var cronTrigger =
            (ICronTrigger)WorkflowTrigger
                .UsingJobData(workflowSpec.Path, WorkflowMode.Job)
                .WithCronSchedule(workflowSpec.Cron)
                .Build();

        about.Tick.LogDebug("Trigger '{workflow.trigger.key}': {workflow.trigger.cron}", cronTrigger.Key, cronTrigger.CronExpressionString);

        // core: Compare cron expressions only if the workflow is already scheduled.
        if (await scheduler.GetTrigger(cronTrigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
        {
            // core: Compare both the cron expressions.
            if (cronTrigger is { CronExpressionString: { } otherCron } && currentCron.Equals(otherCron))
            {
                about.Last.LogInformation("Workflow '{@workflow.name}' has not changed.");
            }
            else
            {
                if (await scheduler.RescheduleJob(cronTrigger.Key, cronTrigger) is { } next)
                {
                    about.Last.LogInformation
                    (
                        "Reschedules workflow '{@workflow.name}' to '{workflow.trigger.cron}'. Next execution at '{workflow.trigger.next}'.",
                        workflowSpec.Path,
                        cronTrigger.CronExpressionString,
                        next
                    );
                }
                else
                {
                    about.Last.LogError("Cannot reschedule '{@workflow.name}'.");
                }
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
                workflowSpec.Path,
                cronTrigger.CronExpressionString,
                next
            );
        }
    }

    public async Task AsOnce(WorkflowPattern workflowPattern, WorkflowOverride workflowOverride)
    {
        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            var trigger =
                WorkflowTrigger
                    .UsingJobData(workflowPath, WorkflowMode.Api)
                    .UsingJobData(nameof(WorkflowOverride), workflowOverride.Steps)
                    .StartAt(workflowOverride.StartAt)
                    .WithSimpleSchedule(x => x.WithRepeatCount(0))
                    .Build();

            if (await loadWorkflowSpec.FromFile(workflowPath) is { } workflow)
            {
                await AsOnce(workflow, trigger, [.. workflowOverride.Steps]);
            }
            else
            {
                logger.Snap<ScheduleWorkflow>().Baseline.LogError("Skips workflow '{@workflow.name}' because it does not load.");
            }
        }
    }

    private async Task AsOnce(WorkflowSpec workflowSpec, ITrigger trigger, IImmutableList<string> steps)
    {
        using var about = logger.Baseline.BeginScopeAbout<ScheduleWorkflow>
        ([
            ("@workflow.name", workflowSpec.Path),
        ]);
        about.Zero.Internal.LogDebug(null);


        var scheduler = await schedulerFactory.GetScheduler();
        if (await scheduler.GetTrigger(trigger.Key) is not null)
        {
            if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
            {
                about.Last.LogInformation
                (
                    "Reschedules {@workflow.name}. Next execution at '{workflow.trigger.next}'.",
                    workflowSpec.Path,
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
                    .WithIdentity(trigger.JobKey)
                    .DisallowConcurrentExecution()
                    .Build();

            var nextNew = await scheduler.ScheduleJob(jobDetail, trigger);
            about.Last.LogInformation(
                "Schedules workflows '{@workflow.name}'. Next execution at '{workflow.trigger.next}'.",
                workflowSpec.Path,
                nextNew
            );
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