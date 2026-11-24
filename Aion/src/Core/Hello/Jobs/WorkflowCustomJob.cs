using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
using Aion.Data.JobInputs;
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
) : IJob<WorkflowCustomInput>
{
    public async ValueTask Execute(IJobExecutionContext context, WorkflowCustomInput input, CancellationToken cancellationToken = default)
    {
        using var about = logger.Internal.BeginScopeAbout<WorkflowCustomJob>
        ([
            ("job.key", context.JobDetail.Key),
            ("workflow.name", input.WorkflowPath.Name),
            ("workflow.mode", input.WorkflowMode)
        ]);

        about.Zero.LogDebug("Job '{job.key}' is executing.");

        try
        {
            if (await loadWorkflow.Spec(input.WorkflowPath) is { } workflow)
            {
                await executeWorkflow.Invoke(workflow, input.WorkflowMode, input.WorkflowSteps, cancellationToken);
            }

            about.Last.LogInformation("Job '{job.key}' completes in {activity.duration_ms:N0} ms.");
        }
        catch (OperationCanceledException)
        {
            about.Last.LogError("Job '{job.key}' was canceled after {activity.duration_ms:N0} ms.");
        }
        catch (Exception ex)
        {
            // note: This should never happen.
            about.Last.LogError(ex, "Job '{job.key}' fails after {activity.duration_ms:N0} ms.");
            throw new JobExecutionException(ex);
        }
    }
}