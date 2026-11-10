using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Wiretap.Core;

namespace Aion.Core.Magic.Downtimes;

public class EndDowntime
(
    ILogger<EndDowntime> logger,
    FindWorkflows findWorkflows
)
{
    public async Task<IImmutableList<WorkflowDowntime>> Now(string? workflowPattern)
    {
        using var scope = logger.BeginScopeWith(("workflow.pattern", workflowPattern));

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