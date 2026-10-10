using System;
using System.IO;
using System.Text.Json.Serialization;

namespace Aion.Util.Data.Scheduler;

public record SchedulerOptions
{
    public string Cron { get; init; } = null!;

    public TimeSpan StartDelay { get; init; }

    public string Home { get; set; } = null!;

    [JsonIgnore]
    public SchedulerDirectory HomeDirectory => new(Path.GetFullPath(Home));

    [JsonIgnore]
    public string Name => Path.GetDirectoryName(Home)!;
}

public record SchedulerDirectory(string Path)
{
    public string LoggersDirectory => System.IO.Path.Combine(Path, "loggers");
    public string WorkflowsDirectory => System.IO.Path.Combine(Path, "workflows");
}