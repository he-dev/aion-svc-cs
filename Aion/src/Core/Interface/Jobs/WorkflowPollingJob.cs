using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Objective.Workflows;
using Aion.Meta.Logging.Wiretap;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Interface.Jobs;

internal class WorkflowPollingJob
(
    ILogger<WorkflowPollingJob> logger,
    ScheduleWorkflow scheduleWorkflow
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScopeWith
        (
            ("aion.job.group", context.JobDetail.Key.Group),
            ("aion.job.name", context.JobDetail.Key.Name)
        );

        logger.Buzz.Zero.LogDebug("Executing '{aion.job.group}:{aion.job.name}'.", context.JobDetail.Key.Group, context.JobDetail.Key.Name);

        try
        {
            await scheduleWorkflow.Directory();
            logger.Buzz.Okay.LogDebug("Job '{aion.job.group}:{aion.job.name}' completed.", context.JobDetail.Key.Group, context.JobDetail.Key.Name);
        }
        catch (Exception ex)
        {
            logger.Buzz.Fail.LogError(ex, "Job '{aion.job.group}:{aion.job.name}' failed.", context.JobDetail.Key.Group, context.JobDetail.Key.Name);
            throw new JobExecutionException(ex);
        }
    }
}