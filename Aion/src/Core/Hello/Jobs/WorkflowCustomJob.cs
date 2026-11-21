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
        var workflowPath = context.Trigger.JobDataMap.Get<WorkflowPath>(JobDataKeys.WorkflowPath);
        var workflowSteps = context.Trigger.JobDataMap.Get<IImmutableList<string>>(JobDataKeys.WorkflowSteps);

        using var scope = logger.Internal.BeginScopeAbout<WorkflowCustomJob>
        ([
            ("job.key", context.JobDetail.Key),
            ("@workflow.name", workflowPath),
            ("workflow.trigger.source", WorkflowTriggerSource.Cli)
        ]);

        scope.Zero.Internal.LogDebug("Job '{job.key}' is executing.");

        try
        {
            if (await loadWorkflowSpec.FromFile(workflowPath) is { } workflow)
            {
                await executeWorkflow.Invoke(workflow, WorkflowTriggerSource.Cli, workflowSteps, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            scope.Last.Internal.LogError("Job '{job.key}' was canceled.");
        }
        catch (Exception ex)
        {
            // note: This should never happen.
            scope.Last.Internal.LogError(ex, "Job '{job.key}' fails.");
            throw new JobExecutionException(ex);
        }
    }
}