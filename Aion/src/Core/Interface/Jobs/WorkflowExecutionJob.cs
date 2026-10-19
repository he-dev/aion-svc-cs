using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Objective.Workflows;
using Aion.Meta.Logging.Wiretap;
using Aion.Meta.Quartz;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Interface.Jobs;

public class WorkflowExecutionJob
(
    ILogger<WorkflowExecutionJob> logger,
    ComposeWorkflow composeWorkflow,
    ExecuteWorkflow executeWorkflow,
    ScheduleWorkflow scheduleWorkflow
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var workflowPath = context.Trigger.JobDataMap.Get<WorkflowPath>();
        var workflowMode = context.Trigger.JobDataMap.Get<WorkflowMode>();

        using var scope = logger.BeginScopeWith
        (
            ("aion.job.group", context.JobDetail.Key.Group),
            ("aion.job.name", context.JobDetail.Key.Name),
            ("aion.workflow.path", workflowPath),
            ("aion.workflow.mode", workflowMode)
        );

        logger.Buzz.LogDebug("Job '{aion.job.group}:{aion.job.name}' is executing.");

        try
        {
            // core: Needs to restore the user-trigger in case one was used.
            var patch =
                workflowMode == WorkflowMode.User
                    ? new WorkflowPatch { Trigger = context.Trigger }
                    : WorkflowPatch.Empty;

            if (await composeWorkflow.From(workflowPath, patch) is {} workflow)
            {
                var progress = new ExecuteWorkflowProgress();
                await executeWorkflow.Now(workflow, progress, cancellationToken);
            }
            else
            {
                //await scheduleWorkflow.At<Workflow.Yuck>(context.Trigger);
            }
        }
        catch (OperationCanceledException)
        {
            logger.Buzz.LogError("Job '{aion.job.group}:{aion.job.name}' was canceled.");
        }
        catch (Exception ex)
        {
            // note: This should never happen.
            logger.Buzz.LogError(ex, "Job '{aion.job.group}:{aion.job.name}' failed.");
            //await scheduleWorkflow.At<Workflow.Yuck>(context.Trigger);
            throw new JobExecutionException(ex);
        }
    }
}