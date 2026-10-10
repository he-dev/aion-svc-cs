using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Objective.Workflows;
using Aion.Meta.Logging;
using Aion.Meta.Quartz;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Interface.Jobs;

public class WorkflowExecutionJob
(
    ILogger<WorkflowExecutionJob> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    GetWorkflow getWorkflow,
    ExecuteWorkflow executeWorkflow
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var workflowPath = context.Trigger.JobDataMap.GetString(nameof(WorkflowPath))!;
        var workflowMode = context.Trigger.JobDataMap.GetEnum<WorkflowMode>();

        using var scope = logger.BeginScopeFrom(new
        {
            JobName = context.JobDetail.Key.Name,
            WorkflowPath = workflowPath,
            WorkflowMode = workflowMode,
        });

        logger.LogDebug("Executing '{JobName}'.", context.JobDetail.Key.Name);

        try
        {
            var workflow = await getWorkflow.For(new WorkflowPath(schedulerOptions.Value.HomeDirectory.Path, workflowPath), WorkflowPatch.Empty);
            var stepResults = await executeWorkflow.Now(workflow);
            if (workflowMode == WorkflowMode.Cron && !stepResults.Any())
            {
                logger.LogWarning("Unscheduling workflow '{WorkflowPath}' because it does not do anything.", workflowPath);
                await context.Scheduler.DeleteJob(context.JobDetail.Key, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow '{WorkflowPath}' failed.", workflowPath);
            throw new JobExecutionException(ex);
        }
    }
}