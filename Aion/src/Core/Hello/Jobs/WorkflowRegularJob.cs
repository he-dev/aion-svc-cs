using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
using Aion.Data;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Quartz;
using Wiretap.Core;

namespace Aion.Core.Hello.Jobs;

public class WorkflowRegularJob
(
    ILogger<WorkflowRegularJob> logger,
    LoadWorkflowSpec loadWorkflowSpec,
    ExecuteWorkflow executeWorkflow,
    ScheduleWorkflow scheduleWorkflow
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var workflowPath = context.Trigger.JobDataMap.Get<WorkflowPath>(nameof(WorkflowPath));
        var workflowMode = context.Trigger.JobDataMap.Get<WorkflowMode>(nameof(WorkflowMode));

        using var about = logger.Internal.BeginScopeAbout<WorkflowRegularJob>
        ([
            ("job.key", context.JobDetail.Key),
            ("workflow.name", workflowPath.Name),
            ("workflow.mode", workflowMode)
        ]);

        about.Zero.LogDebug(null);

        try
        {
            if (await loadWorkflowSpec.FromFile(workflowPath) is { } workflow)
            {
                await executeWorkflow.Invoke(workflow, WorkflowMode.Job, ImmutableList<string>.Empty, cancellationToken);
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