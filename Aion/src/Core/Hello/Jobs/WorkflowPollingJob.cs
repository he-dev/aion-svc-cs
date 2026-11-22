using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
using Aion.Data;
using Microsoft.Extensions.Logging;
using Quartz;
using Wiretap.Core;

namespace Aion.Core.Hello.Jobs;

internal class WorkflowPollingJob
(
    ILogger<WorkflowPollingJob> logger,
    ScheduleWorkflow scheduleWorkflow
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var about = logger.Internal.BeginScopeAbout<WorkflowPollingJob>
        ([
            ("job.group", context.JobDetail.Key.Group),
            ("job.name", context.JobDetail.Key.Name)
        ]);

        about.Zero.LogDebug(null);

        try
        {
            await scheduleWorkflow.AsCron(WorkflowPattern.Any);
            about.Last.LogInformation("Job '{job.key}' completes in {activity.duration_ms:N0} ms.");
        }
        catch (Exception ex)
        {
            about.Last.LogError(ex, "Job '{job.key}' fails after {activity.duration_ms:N0} ms.");
            throw new JobExecutionException(ex);
        }
    }
}
