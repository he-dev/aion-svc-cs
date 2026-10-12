using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aion.Util.Data;

// note: This class is deserialized from workflow JSON.
public record WorkflowSpec
{
    public bool Disabled { get; init; }

    public string Cron { get; init; } = null!;

    public Dictionary<string, string> Variables { get; init; } = [];

    public string? Logger { get; init; }

    public WorkflowStepSpec[] Steps { get; init; } = null!;

    [JsonIgnore]
    public WorkflowPath Path { get; init; } = null!;

    public record Null : WorkflowSpec
    {
        public Null()
        {
            Disabled = true;
        }
    }
}

public record WorkflowStepSpec
{
    [JsonIgnore]
    public int Index { get; init; } = -1;

    public string? Name { get; init; }

    public bool Disabled { get; init; } = true;

    public string FileName { get; init; } = null!;

    public string[]? Arguments { get; init; }

    public Dictionary<string, string>? Environment { get; init; }

    public string? WorkingDirectory { get; init; }

    public TimeSpan? Timeout { get; init; }
}