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

public class WorkflowRegularJob
(
    ILogger<WorkflowRegularJob> logger,
    ComposeWorkflow composeWorkflow,
    ExecuteWorkflow executeWorkflow,
    ScheduleWorkflow scheduleWorkflow
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var workflowPath = context.Trigger.JobDataMap.Get<WorkflowPath>(JobDataKeys.WorkflowPath);

        using var about = logger.Internal.BeginScopeAbout<WorkflowRegularJob>
        ([
            ("job.key", context.JobDetail.Key),
            ("@workflow.name", workflowPath),
            ("workflow.mode", WorkflowTriggerMode.Cron)
        ]);

        about.Zero.Internal.LogDebug(null);

        try
        {
            if (await composeWorkflow.From(workflowPath) is { } workflow)
            {
                await executeWorkflow.Invoke(workflow, cancellationToken);
            }
            else
            {
                await scheduleWorkflow.Deletion(context.Trigger);
            }

            about.Last.Internal.LogInformation(null);
        }
        catch (OperationCanceledException)
        {
            about.Last.Internal.LogError("Job '{job.key}' was canceled.");
        }
        catch (Exception ex)
        {
            about.Last.Internal.LogError(ex, "Job '{job.key}' fails.");
            throw new JobExecutionException(ex);
        }
    }
}
