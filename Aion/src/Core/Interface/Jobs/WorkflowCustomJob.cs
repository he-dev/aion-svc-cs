using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Objective.Workflows;
using Aion.Meta.Logging.Wiretap;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Interface.Jobs;

public class WorkflowCustomJob
(
    ILogger<WorkflowRegularJob> logger,
    ComposeWorkflow composeWorkflow,
    ExecuteWorkflow executeWorkflow
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var workflowPath = context.Trigger.JobDataMap.Get<WorkflowPath>(JobDataKeys.WorkflowPath);
        var workflowSteps = context.Trigger.JobDataMap.Get<string[]>(JobDataKeys.WorkflowSteps);

        using var scope = logger.BeginScopeWith
        (
            ("aion.job.group", context.JobDetail.Key.Group),
            ("aion.job.name", context.JobDetail.Key.Name),
            ("aion.workflow.path", workflowPath),
            ("aion.workflow.mode", WorkflowTriggerMode.User)
        );

        logger.System.LogDebug("Job '{aion.job.group}:{aion.job.name}' is executing.");

        try
        {
            if (await composeWorkflow.From(workflowPath, new WorkflowPatch { Steps = workflowSteps }) is { } workflow)
            {
                await executeWorkflow.Invoke(workflow, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.System.LogError("Job '{aion.job.group}:{aion.job.name}' was canceled.");
        }
        catch (Exception ex)
        {
            // note: This should never happen.
            logger.System.LogError(ex, "Job '{aion.job.group}:{aion.job.name}' failed.");
            throw new JobExecutionException(ex);
        }
    }
}