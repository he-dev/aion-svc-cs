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
using Wiretap.Core;

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

        logger.LogInformation("Searching for workflows in '{scheduler.home}' matching '{workflow.pattern}'.", workflowsDirectory, pattern);

        var result =
            new Matcher(StringComparison.OrdinalIgnoreCase)
                .AddInclude(pattern)
                .AddExclude("**/.profile.json")
                .AddExclude("loggers/**")
                .Execute(new DirectoryInfoWrapper(new DirectoryInfo(workflowsDirectory)));

        var matches =
            result.Files
                .Select(x => x.Path)
                .Select(path => new WorkflowPath(workflowsDirectory.ToSlash(), path.ToSlash()))
                .OrderBy(x => x.Directory)
                .ThenBy(x => x.Name)
                .ToImmutableList();

        var ambiguous = matches.GroupBy(w => w.Name).ToLookup(g => g.Count() > 1);
        if(ambiguous[true].Any())
        {
            logger.With(Story.Domain).LogError("There are {workflow.count} ambiguous workflows", ambiguous[true].Count());
            foreach (var workflow in ambiguous[true])
            {
                logger
                    .With(Story.System)
                    .LogError("Workflow '{workflow.name}' is ambiguous. Only one extension [json, yaml, yml] per name is allowed.", workflow);
            }
            return ImmutableArray<WorkflowPath>.Empty;
        }

        logger.With(Story.Domain).LogInformation("There are {workflow.count} workflows that match '{workflow.pattern}'.", matches.Count, pattern);

        return [.. ambiguous[false].SelectMany(pathGroup => pathGroup)];
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