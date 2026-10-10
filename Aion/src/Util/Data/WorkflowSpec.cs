using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Aion.Util.Data;

// note: This class is deserialized from workflow JSON.
public record WorkflowSpec
{
    // core: Make the user specify this value explicitly, so they don't activate workflows by accident.
    public bool Enabled { get; init; }

    [MustBeCron]
    public string Cron { get; init; } = null!;

    public Dictionary<string, string>? Variables { get; init; }

    public string? Logger { get; init; }

    public WorkflowStepSpec[] Steps { get; init; } = null!;

    [JsonIgnore]
    public WorkflowPath Path { get; init; } = null!;

    public static WorkflowSpec Disabled { get; } = new() { Enabled = false };
}

public record WorkflowStepSpec
{
    public string? Name { get; init; }

    [JsonIgnore]
    public int Index { get; init; } = -1;

    public bool Enabled { get; init; } = true;

    [NotNullOrWhiteSpace]
    public string FileName { get; init; } = null!;

    [JsonConverter(typeof(StepArgumentConverter))]
    public IImmutableList<StepArgument>? Arguments { get; init; }

    public Dictionary<string, string>? Environment { get; init; }

    public string? WorkingDirectory { get; init; }

    public TimeSpan? Timeout { get; init; }

    public string? OnError { get; init; }
}