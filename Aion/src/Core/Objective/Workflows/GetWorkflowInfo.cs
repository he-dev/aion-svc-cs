using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Meta.Quartz;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Objective.Workflows;

public class GetWorkflowInfo
(
    ILogger<GetWorkflowInfo> logger,
    FindWorkflows findWorkflows,
    GetWorkflow getWorkflow
)
{
    public async Task<object> Where(string? workflowPattern)
    {
        // note: Uses Workflow as the type and not an object so that we can calculate next later and sort them.
        var workflows = ImmutableList<Workflow>.Empty;

        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            var workflow = await getWorkflow.For(workflowPath, WorkflowPatch.Empty);
            workflows = workflows.Add(workflow);
        }

        var utcNow = DateTimeOffset.UtcNow; // note: Keeps the timestamp stable for all items.
        var results =
            from workflow in workflows
            let next = workflow.Trigger.FiresAt(utcNow).Take(3).Select(x => x.ToLocalTime())
            orderby next.FirstOrDefault(), workflow.Path.Group, workflow.Path.Workflow
            select new
            {
                path = workflow.Path,
                isOn = workflow.Enabled,
                okay = workflow.Okay,
                cron = ((ICronTrigger)workflow.Trigger).CronExpressionString,
                next = next,
                steps = new
                {
                    workflow.Steps.Count,
                    enabled = workflow.Steps.Where(s => s.Enabled).Select(s => s.Index)
                }
            };

        return new { results };
    }
}