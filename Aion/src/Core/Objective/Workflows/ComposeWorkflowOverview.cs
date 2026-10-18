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

public class ComposeWorkflowOverview
(
    ILogger<ComposeWorkflowOverview> logger,
    FindWorkflows findWorkflows,
    ComposeWorkflow composeWorkflow
)
{
    public async Task<object> Where(string? workflowPattern)
    {
        // note: Uses Workflow as the type and not an object so that we can calculate next later and sort them.
        var workflows = ImmutableList<Workflow>.Empty;

        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            var workflow = await composeWorkflow.From(workflowPath, WorkflowPatch.Empty);
            workflows = workflows.Add(workflow);
        }

        var utcNow = DateTimeOffset.UtcNow; // note: Keeps the timestamp stable for all items.
        var okay =
            from workflow in workflows.OfType<Workflow.Okay>()
            let next = workflow.Trigger.FiresAt(utcNow).Take(3).Select(x => x.ToLocalTime())
            orderby next.FirstOrDefault(), workflow.Spec.Path.Directory, workflow.Spec.Path.Workflow
            select new
            {
                path = workflow.Spec.Path,
                isOn = workflow.Spec.Disabled == false,
                cron = ((ICronTrigger)workflow.Trigger).CronExpressionString,
                next = next,
                steps =
                    from step in workflow.Steps
                    select new
                    {
                        step.Spec.Index,
                        step.Spec.Name,
                    }
            };

        var yuck =
            from workflow in workflows.OfType<Workflow.Yuck>()
            select new { workflow.Spec.Path, workflow.Spec.Issues };

        return new { okay, yuck };
    }
}