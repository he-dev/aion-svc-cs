using System;
using System.Threading.Tasks;
using Aion.Core.Hello.Jobs;
using Aion.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Wiretap.Core;

namespace Aion.Core.Magic.Workflows;

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

        try
        {
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
            logger
                .Baseline
                .Snap<ScheduleWorkflowPolling>()
                .LogInformation("Workflow polling starts. Next run at '{workflow.polling.next}'.", next);
        }
        catch (Exception ex)
        {
            logger
                .Baseline
                .Snap<ScheduleWorkflowPolling>()
                .LogError(ex, "Workflow polling fails.");

            throw new JobExecutionException(ex);
        }
    }
}