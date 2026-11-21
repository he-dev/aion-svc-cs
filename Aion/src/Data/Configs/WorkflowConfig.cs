using System;
using System.Collections.Generic;

namespace Aion.Data.Configs;

// note: This class is deserialized from workflow JSON.
public record WorkflowConfig
{
    public StepConfig[] Steps { get; init; } = [];

    public bool Enabled { get; init; }

    public string Cron { get; init; } = null!;

    public Dictionary<string, string> Variables { get; init; } = new();

    public string? Logger { get; init; }
}

public record StepConfig
{
    public string? Name { get; init; }

    public bool Enabled { get; init; }

    public string FileName { get; init; } = null!;

    public string[] Arguments { get; init; } = [];

    public Dictionary<string, string> Environment { get; init; } = new();

    public string? WorkingDirectory { get; init; }

    public TimeSpan Timeout { get; init; } = System.Threading.Timeout.InfiniteTimeSpan;
}