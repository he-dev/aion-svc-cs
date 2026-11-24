using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
using Aion.Data.JobInputs;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Quartz;
using Wiretap.Core;

namespace Aion.Core.Hello.Jobs;

public class WorkflowRegularJob
(
    ILogger<WorkflowRegularJob> logger,
    LoadWorkflow loadWorkflow,
    ExecuteWorkflow executeWorkflow,
    ScheduleWorkflow scheduleWorkflow
) : IJob<WorkflowRegularInput>
{
    public async ValueTask Execute(IJobExecutionContext context, WorkflowRegularInput input, CancellationToken cancellationToken = default)
    {
        using var about = logger.Internal.BeginScopeAbout<WorkflowRegularJob>
        ([
            ("job.key", context.JobDetail.Key),
            ("workflow.name", input.WorkflowPath.Name),
            ("workflow.mode", input.WorkflowMode)
        ]);

        about.Zero.LogDebug(null);

        try
        {
            if (await loadWorkflow.Spec(input.WorkflowPath) is { } workflow)
            {
                await executeWorkflow.Invoke(workflow, input.WorkflowMode, ImmutableList<string>.Empty, cancellationToken);
            }
            else
            {
                await scheduleWorkflow.Deletion(context.Trigger);
            }

            about.Last.LogInformation("Job '{job.key}' completes in {activity.duration_ms:N0} ms.");
        }
        catch (OperationCanceledException)
        {
            about.Last.LogError("Job '{job.key}' was canceled after {activity.duration_ms:N0} ms.");
        }
        catch (Exception ex)
        {
            about.Last.LogError(ex, "Job '{job.key}' fails after {activity.duration_ms:N0} ms.");
            throw new JobExecutionException(ex);
        }
    }
}