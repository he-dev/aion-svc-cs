using System;
using System.Threading.Tasks;
using Aion.Core.Objective;
using Aion.Meta.Logging;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Interface.Jobs;

[DisallowConcurrentExecution]
internal class WorkflowPollingJob
(
    ILogger<WorkflowPollingJob> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    SynchronizeWorkflows synchronizeWorkflows
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        using var scope = logger.BeginScopeFrom(new
        {
            JobName = context.JobDetail.Key.Name,
            ProfileName = profileName
        });

        logger.LogDebug("Executing '{JobName}'.", context.JobDetail.Key.Name);
        try
        {
            await synchronizeWorkflows.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job '{JobName}' failed.", context.JobDetail.Key.Name);
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}