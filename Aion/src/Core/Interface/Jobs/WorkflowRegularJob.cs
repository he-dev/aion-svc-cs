using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Objective.Workflows;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;
using Quartz;
using Wiretap.Core;

namespace Aion.Core.Interface.Jobs;

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
        ([
            ("job.group", context.JobDetail.Key.Group),
            ("job.name", context.JobDetail.Key.Name),
            ("workflow.path", workflowPath),
            ("workflow.mode", WorkflowTriggerMode.Cron)
        ]);

        about.Zero.With(Story.System).LogDebug(null);

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

            about.Zero.With(Story.System).LogInformation(null);
        }
        catch (OperationCanceledException)
        {
            about.Zero.With(Story.System).LogError("Job '{job.group}:{job.name}' was canceled.");
        }
        catch (Exception ex)
        {
            about.Zero.With(Story.System).LogError(ex, "Job '{job.group}:{job.name}' failed.");
            throw new JobExecutionException(ex);
        }
    }
}