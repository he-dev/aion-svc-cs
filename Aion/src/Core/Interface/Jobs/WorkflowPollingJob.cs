using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Objective;
using Aion.Meta.Logging;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Interface.Jobs;

internal class WorkflowPollingJob
(
    ILogger<WorkflowPollingJob> logger,
    SynchronizeWorkflows synchronizeWorkflows
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScopeWith
        (
            ("aion.job.group", context.JobDetail.Key.Group),
            ("aion.job.name", context.JobDetail.Key.Name)
        );

        logger.LogDebug("Executing '{aion.job.group}:{aion.job.name}'.", context.JobDetail.Key.Group, context.JobDetail.Key.Name);

        try
        {
            await synchronizeWorkflows.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job '{aion.job.group}:{aion.job.name}' failed.", context.JobDetail.Key.Group, context.JobDetail.Key.Name);
            throw new JobExecutionException(ex);
        }
    }
}