using System;
using System.IO;
using System.Text.Json.Serialization;

namespace Aion.Util.Data.Scheduler;

public record SchedulerOptions
{
    public WorkflowPolling Polling { get; init; } = new();

    public TimeSpan StartDelay { get; init; }

    public string Home { get; set; } = null!;

    [JsonIgnore]
    public string Name => Path.GetDirectoryName(Home)!;
}

public record WorkflowPolling
{
    public string Cron { get; init; } = null!;
    public bool Disabled { get; init; }
}
