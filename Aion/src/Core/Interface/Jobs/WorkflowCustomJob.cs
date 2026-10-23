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
        var workflowMod = context.Trigger.JobDataMap.Get<WorkflowMod>(JobDataKeys.WorkflowMod);

        using var scope = logger.BeginScopeWith
        (
            ("job.group", context.JobDetail.Key.Group),
            ("job.name", context.JobDetail.Key.Name),
            ("@workflow.name", workflowPath),
            ("workflow.mode", WorkflowTriggerMode.User)
        );

        logger.System.LogDebug("Job '{job.group}:{job.name}' is executing.");

        try
        {
            if (await composeWorkflow.From(workflowPath, workflowMod) is { } workflow)
            {
                await executeWorkflow.Invoke(workflow, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.System.LogError("Job '{job.group}:{job.name}' was canceled.");
        }
        catch (Exception ex)
        {
            // note: This should never happen.
            logger.System.LogError(ex, "Job '{job.group}:{job.name}' failed.");
            throw new JobExecutionException(ex);
        }
    }
}