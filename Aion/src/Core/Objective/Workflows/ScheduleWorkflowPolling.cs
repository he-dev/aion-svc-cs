using System;
using System.Threading.Tasks;
using Aion.Core.Interface.Jobs;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Objective.Workflows;

public class ScheduleWorkflowPolling
(
    ILogger<ScheduleWorkflowPolling> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    ISchedulerFactory schedulerFactory
)
{
    public async Task Invoke()
    {
        var scheduler = await schedulerFactory.GetScheduler();
        var schedulerHome = schedulerOptions.Value.Home;

        var jobDetail = JobBuilder
            .Create<WorkflowPollingJob>()
            .WithIdentity("workflow-polling", schedulerHome)
            .StoreDurably()
            .Build();

        await scheduler.AddJob(jobDetail);

        // core: Schedule cron sync.
        var next = await scheduler.ScheduleJob(
            TriggerBuilder
                .Create()
                .ForJob(jobDetail)
                .WithIdentity("workflow-polling", schedulerHome)
                .WithCronSchedule(schedulerOptions.Value.Polling.Cron)
                .StartAt(DateTimeOffset.UtcNow + schedulerOptions.Value.StartDelay)
                .Build()
        );

        logger.LogInformation("Workflow polling for '{aion.scheduler.home}' scheduled at '{Next}'.", schedulerHome, next);
    }
}