using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Meta.Logging.Wiretap;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Objective.Downtimes;

public class GetDowntimes
(
    ILogger<GetDowntimes> logger,
    FindWorkflows findWorkflows
)
{
    public async Task<ImmutableList<WorkflowDowntime>> Where(string? workflowPattern)
    {
        using var scope = logger.BeginScopeFrom(new { workflowPattern });

        var workflowDowntimes = new List<WorkflowDowntime>();

        foreach (var workflowPath in findWorkflows.Where(workflowPattern))
        {
            try
            {
                if (await WorkflowDowntime.FromFile(workflowPath) is { } lockFile)
                {
                    workflowDowntimes.Add(lockFile);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load lock file '{LockFileName}'.", workflowPath);
            }
        }

        return workflowDowntimes.ToImmutableList();
    }
}