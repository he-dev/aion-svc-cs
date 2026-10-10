using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Objective.Downtimes;

public class EndDowntime
(
    ILogger<EndDowntime> logger,
    FindWorkflows findWorkflows
)
{
    public async Task<IImmutableList<WorkflowDowntime>> Now(string? workflowPattern)
    {
        using var scope = logger.BeginScopeFrom(new { workflowPattern });

        var workflowDowntimes = new List<WorkflowDowntime>();

        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            try
            {
                if (await WorkflowDowntime.FromFile(workflowPath) is { } workflowDowntime)
                {
                    await workflowDowntime.EndsNow();
                    workflowDowntimes.Add(workflowDowntime);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to end downtime for '{WorkflowPath}'.", workflowPath);
            }
        }

        return workflowDowntimes.ToImmutableList();
    }
}