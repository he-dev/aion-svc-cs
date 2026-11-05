using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Objective.Workflows;
using Microsoft.Extensions.Logging;
using Quartz;
using Wiretap.Core;

namespace Aion.Core.Interface.Jobs;

internal class WorkflowPollingJob
(
    ILogger<WorkflowPollingJob> logger,
    ScheduleWorkflow scheduleWorkflow
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScopeAbout<WorkflowPollingJob>
        ([
            ("job.group", context.JobDetail.Key.Group),
            ("job.name", context.JobDetail.Key.Name)
        ]);

        scope.Zero.With(Story.System).LogDebug("Executing '{job.group}:{job.name}'.");

        try
        {
            await scheduleWorkflow.AsCron();
            scope.Tick.With(Story.System).LogDebug("Job '{job.group}:{job.name}' completed.");
        }
        catch (Exception ex)
        {
            scope.Last.With(Story.System).LogError(ex, "Job '{job.group}:{job.name}' failed.");
            throw new JobExecutionException(ex);
        }
    }
}