using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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

        var schedulerDirectory = schedulerOptions.Value.HomeDirectory.Path;

        logger.LogInformation("Searching for workflows in '{Path}' matching '{Pattern}'.", schedulerDirectory, pattern);

        var result =
            new Matcher(StringComparison.OrdinalIgnoreCase)
                .AddInclude(pattern)
                .AddExclude("**/.profile.json")
                .AddExclude("loggers/**")
                .Execute(new DirectoryInfoWrapper(new DirectoryInfo(schedulerDirectory)));

        var workflows =
            result.Files
                .Select(x => x.Path)
                .Select(path => new WorkflowPath(schedulerDirectory, path))
                .OrderBy(x => x.Group)
                .ThenBy(x => x.Workflow)
                .ToImmutableList();

        logger.LogDebug("Found {Count} workflows matching '{Pattern}'.", workflows.Count, pattern);

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