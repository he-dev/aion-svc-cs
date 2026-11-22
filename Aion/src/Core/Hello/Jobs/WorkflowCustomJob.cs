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

public class WorkflowCustomJob
(
    ILogger<WorkflowRegularJob> logger,
    LoadWorkflowSpec loadWorkflowSpec,
    ExecuteWorkflow executeWorkflow
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var workflowPath = context.Trigger.JobDataMap.Get<WorkflowPath>(nameof(WorkflowPath));
        var workflowSteps = context.Trigger.JobDataMap.Get<IImmutableList<string>>(nameof(WorkflowOverride));
        var workflowMode = context.Trigger.JobDataMap.Get<WorkflowMode>(nameof(WorkflowMode));

        using var about = logger.Internal.BeginScopeAbout<WorkflowCustomJob>
        ([
            ("job.key", context.JobDetail.Key),
            ("workflow.name", workflowPath.Name),
            ("workflow.mode", workflowMode)
        ]);

        about.Zero.LogDebug("Job '{job.key}' is executing.");

        try
        {
            if (await loadWorkflowSpec.FromFile(workflowPath) is { } workflow)
            {
                await executeWorkflow.Invoke(workflow, WorkflowMode.Cli, workflowSteps, cancellationToken);
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