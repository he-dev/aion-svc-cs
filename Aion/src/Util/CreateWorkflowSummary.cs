using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;
using Wiretap.Data;

namespace Aion.Util;

public class CreateWorkflowSummary
(
    ILogger<CreateWorkflowSummary> logger,
    ForkLogging forkLogging,
    FindWorkflows findWorkflows,
    LoadWorkflow loadWorkflow
)
{
    public async Task<object> Where(string? workflowPattern)
    {
        // note: Uses Workflow as the type and not an object so that we can calculate next later and sort them.
        var summaries = ImmutableList<object>.Empty;

        var utcNow = DateTimeOffset.UtcNow; // note: Keeps the timestamp stable for all items.

        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            var inMemoryFork = new InMemoryFork();
            using var unsubscribe = forkLogging.Into(inMemoryFork);

            if (await loadWorkflow.From(workflowPath) is { } workflow)
            {
                var next = workflow.Trigger.FiresAt(utcNow).Take(3).Select(x => x.ToLocalTime());
                summaries = summaries.Add(new
                {
                    path = workflow.Spec.Path.Name,
                    isOn = workflow.Enabled,
                    cron = ((ICronTrigger)workflow.Trigger).CronExpressionString,
                    next = next,
                    steps = workflow.Steps.Count
                });
            }
            else
            {
                var logs =
                    inMemoryFork
                        .AtLeast(LogLevel.Information)
                        //.WhereProperty("content", Content.Baseline)
                        .Format("{Level:u3} [{activity.event}] {Message:lj}")
                        .ToList();

                summaries = summaries.Add(new
                {
                    path = workflowPath.Name,
                    logs = logs,
                });
            }
        }


        return new { workflows = summaries };
    }
}