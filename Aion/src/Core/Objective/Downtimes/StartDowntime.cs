using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Meta.Logging.Wiretap;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Objective.Downtimes;

public class StartDowntime
(
    ILogger<StartDowntime> logger,
    FindWorkflows findWorkflows
)
{
    public async Task<IImmutableList<StartDowntimeResult>> Now(string? workflowPattern, WorkflowDowntime downtime)
    {
        using var scope = logger.BeginScopeFrom(new { workflowPattern });
        try
        {
            var workflowPaths = new List<StartDowntimeResult>();
            foreach (var workflowPath in findWorkflows.Where(workflowPattern))
            {
                try
                {
                    await downtime.ToFile(workflowPath);
                    workflowPaths.Add(new StartDowntimeResult(workflowPath));
                    logger.LogInformation("Workflow '{WorkflowPath}' has been locked for {Duration} starting at {StartsOnUtc} (UTC).", workflowPath, downtime.Duration, downtime.StartsOnUtc);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unable to lock '{WorkflowPath}'.", workflowPath);
                    workflowPaths.Add(new StartDowntimeResult(workflowPath) { Exception = ex });
                }
            }

            return workflowPaths.ToImmutableList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to lock '{WorkflowPattern}'.", workflowPattern);
            //return Problem(detail: ex.ToString(), statusCode: 500);
            throw;
        }
    }
}

public record StartDowntimeResult(WorkflowPath WorkflowPath)
{
    public Exception? Exception { get; init; }
}