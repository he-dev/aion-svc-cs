using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Aion.Meta;
using Aion.Util.Data;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Util.Buzz;

public class FindWorkflows
(
    ILogger<FindWorkflows> logger,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    public IImmutableList<WorkflowPath> Where(string? pattern = null)
    {
        pattern = NormalizePattern(pattern);

        // core: Use the home directory as the base path and not the "home/workflows" so that "workflows" is part of the group name. Otherwise, it would be empty.
        var workflowsDirectory = Path.Join(schedulerOptions.Value.Home, "workflows");

        logger.LogInformation("Searching for workflows in '{aion.scheduler.home}' matching '{aion.workflow.pattern}'.", workflowsDirectory, pattern);

        var result =
            new Matcher(StringComparison.OrdinalIgnoreCase)
                .AddInclude(pattern)
                .AddExclude("**/.profile.json")
                .AddExclude("loggers/**")
                .Execute(new DirectoryInfoWrapper(new DirectoryInfo(workflowsDirectory)));

        var workflows =
            result.Files
                .Select(x => x.Path)
                .Select(path => new WorkflowPath(workflowsDirectory.ToSlash(), path.ToSlash()))
                .OrderBy(x => x.Directory)
                .ThenBy(x => x.Name)
                .ToImmutableList();

        logger.LogDebug("Found {aion.workflows.count} workflows matching '{aion.workflow.pattern}'.", workflows.Count, pattern);

        return workflows;
    }

    private static string NormalizePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "**/*.json";
        }

        if (!pattern.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return $"{pattern}.json";
        }

        return pattern;
    }
}