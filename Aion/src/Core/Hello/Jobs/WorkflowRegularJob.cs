using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
using Aion.Data;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Quartz;
using Wiretap.Core;
using Wiretap.Util;

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

        using var about = logger.BeginScopeAbout<WorkflowRegularJob>
        (Channel.Internal, [
            ("job.group", context.JobDetail.Key.Group),
            ("job.name", context.JobDetail.Key.Name),
            ("workflow.path", workflowPath),
            ("workflow.mode", WorkflowTriggerMode.Cron)
        ]);

        about.Zero.With(Channel.Internal).LogDebug(null);

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

            about.Last.With(Channel.Internal).LogInformation(null);
        }
        catch (OperationCanceledException)
        {
            about.Last.With(Channel.Internal).LogError("Job '{job.group}:{job.name}' was canceled.");
        }
        catch (Exception ex)
        {
            about.Last.With(Channel.Internal).LogError(ex, "Job '{job.group}:{job.name}' failed.");
            throw new JobExecutionException(ex);
        }
    }
}
