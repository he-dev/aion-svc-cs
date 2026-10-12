using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core.Objective.Workflows;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Objective;

public class SynchronizeWorkflows
(
    ILogger<SynchronizeWorkflows> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    FindWorkflows findWorkflows,
    ComposeWorkflow composeWorkflow,
    ScheduleWorkflow scheduleWorkflow
)
{
    // meta: Cannot be IAsyncEnumerable because of try/catch.
    public async Task<IImmutableList<ScheduleChange>> Invoke()
    {
        using var activity = new Activity(nameof(SynchronizeWorkflows)).Start();
        logger.LogInformation("Synchronizing workflows in '{SchedulerHome}'.", schedulerOptions.Value.Home);

        try
        {
            var progress = new ScheduleWorkflowProgress();
            foreach (var workflowPath in findWorkflows.Where())
            {
                if (await composeWorkflow.From(workflowPath, WorkflowPatch.Empty) is { Disabled: false } workflow)
                {
                    await scheduleWorkflow.Invoke(workflow, progress);
                }
            }

            activity.SetStatus(ActivityStatusCode.Ok).Stop();
            logger.LogInformation("Synchronization of '{SchedulerHome}' finished in {Duration:N0}.", schedulerOptions.Value.Home, activity.Duration);

            return [.. progress];
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Synchronization of '{SchedulerHome}' failed.", schedulerOptions.Value.Home);
            throw;
        }
    }
}

