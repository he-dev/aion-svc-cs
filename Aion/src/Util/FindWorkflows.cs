using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Aion.Data;
using Aion.Data.Scheduler;
using Aion.Util.Dotnet;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiretap.Core;

namespace Aion.Util;

public class FindWorkflows
(
    ILogger<FindWorkflows> logger,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    public IImmutableList<WorkflowPath> Where(WorkflowPattern pattern)
    {
        // core: Use the home directory as the base path and not the "home/workflows" so that "workflows" is part of the group name. Otherwise, it would be empty.
        var workflowsDirectory = Path.Join(schedulerOptions.Value.Home, "workflows");

        logger.LogInformation("Searching for workflows in '{scheduler.home}' matching '{workflow.pattern}'.", workflowsDirectory, pattern);

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns([.. pattern]);
        matcher.AddExcludePatterns(["**/.profile.json", "loggers/**"]);

        var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(workflowsDirectory)));

        var matches =
            result.Files
                .Select(x => x.Path)
                .Select(path => new WorkflowPath(workflowsDirectory.ToSlash(), path.ToSlash()))
                .OrderBy(x => x.Directory)
                .ThenBy(x => x.Relative)
                .ToImmutableList();

        var ambiguous = matches.GroupBy(w => w.Name).ToLookup(g => g.Count() > 1);
        if (ambiguous[true].Any())
        {
            foreach (var workflow in ambiguous[true])
            {
                logger
                    .Baseline
                    .With(("workflow.count", workflow.Count()))
                    .LogError("Workflow '{workflow.name}' is ambiguous. Only one extension [.json, .yaml, .yml] per name is allowed.", workflow.Key);
            }
        }

        logger.Baseline.LogInformation("There are {workflow.count} workflows that match '{workflow.pattern}'.", matches.Count, pattern.Value);

        return [.. ambiguous[false].SelectMany(pathGroup => pathGroup)];
    }
}