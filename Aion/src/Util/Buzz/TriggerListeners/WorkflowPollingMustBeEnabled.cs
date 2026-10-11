using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Util.Buzz.TriggerListeners;

public class WorkflowPollingMustBeEnabled
(
    ILogger<WorkflowPollingMustBeEnabled> logger,
    IOptions<SchedulerOptions> schedulerOptions
) : ITriggerListener
{
    public string Name => nameof(WorkflowPollingMustBeEnabled);

    public async ValueTask<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = new())
    {
        if (schedulerOptions.Value.Polling.Disabled)
        {
            using var scope = logger.BeginScopeFrom(new { TriggerName = trigger.Key.Name });
            logger.LogWarning("Workflow polling from '{aion.scheduler.home}' is disabled - pausing it.", schedulerOptions.Value.Home);
            await context.Scheduler.PauseJob(context.JobDetail.Key, cancellationToken);
            return true;
        }

        return false;
    }

    public ValueTask TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Workflow synchronization trigger '{aion.trigger.group}:{aion.trigger.name}' has fired.", trigger.Key.Group, trigger.Key.Name);
        return ValueTask.CompletedTask;
    }

    public ValueTask TriggerMisfired(ITrigger trigger, IScheduler scheduler, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Workflow synchronization trigger '{aion.trigger.group}:{aion.trigger.name}' has misfired.", trigger.Key.Group, trigger.Key.Name);
        return ValueTask.CompletedTask;
    }

    public static IReadOnlyCollection<IMatcher<TriggerKey>> Matchers { get; } = [NameMatcher<TriggerKey>.NameEquals("workflow-polling")];
}