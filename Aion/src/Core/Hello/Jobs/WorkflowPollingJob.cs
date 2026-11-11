using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
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
        using var scope = logger.Internal.BeginScopeAbout<WorkflowPollingJob>
        ([
            ("job.group", context.JobDetail.Key.Group),
            ("job.name", context.JobDetail.Key.Name)
        ]);

        scope.Zero.LogDebug("Executing '{job.group}:{job.name}'.");

        try
        {
            await scheduleWorkflow.AsCron();
            scope.Last.LogDebug("Job '{job.group}:{job.name}' completed.");
        }
        catch (Exception ex)
        {
            scope.Last.LogError(ex, "Job '{job.group}:{job.name}' failed.");
            throw new JobExecutionException(ex);
        }
    }
}
