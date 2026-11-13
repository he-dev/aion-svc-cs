using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
using Aion.Data;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Quartz;
using Wiretap.Core;

namespace Aion.Core.Hello.Jobs;

public class WorkflowCustomJob
(
    ILogger<WorkflowRegularJob> logger,
    LoadWorkflow loadWorkflow,
    ExecuteWorkflow executeWorkflow
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var workflowPath = context.Trigger.JobDataMap.Get<WorkflowPath>(JobDataKeys.WorkflowPath);
        var workflowMod = context.Trigger.JobDataMap.Get<WorkflowOverride>(JobDataKeys.WorkflowMod);

        using var scope = logger.Internal.BeginScopeAbout<WorkflowCustomJob>
        ([
            ("job.group", context.JobDetail.Key.Group),
            ("job.name", context.JobDetail.Key.Name),
            ("@workflow.name", workflowPath),
            ("workflow.mode", WorkflowTriggerMode.User)
        ]);

        scope.Zero.Internal.LogDebug("Job '{job.group}:{job.name}' is executing.");

        try
        {
            if (await loadWorkflow.From(workflowPath, workflowMod) is { } workflow)
            {
                await executeWorkflow.Invoke(workflow, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            scope.Last.Internal.LogError("Job '{job.group}:{job.name}' was canceled.");
        }
        catch (Exception ex)
        {
            // note: This should never happen.
            scope.Last.Internal.LogError(ex, "Job '{job.group}:{job.name}' fails.");
            throw new JobExecutionException(ex);
        }
    }
}