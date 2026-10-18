using System;
using System.Collections.Immutable;

namespace Aion.Util.Data;

public abstract record WorkflowSpec
{
    public required WorkflowPath Path { get; init; }

    public record Okay : WorkflowSpec
    {
        public bool Disabled { get; init; }

        public required string Cron { get; init; }

        public IImmutableDictionary<string, string> Variables { get; init; } = ImmutableDictionary<string, string>.Empty;

        public string? Logger { get; init; }

        public required IImmutableList<Step> Steps { get; init; }
    }

    public record Yuck : WorkflowSpec
    {
        public required IImmutableList<string> Issues { get; init; } = [];
    }

    public record Step
    {
        public required int Index { get; init; }

        public string? Name { get; init; }

        public bool Disabled { get; init; }

        public required string FileName { get; init; }

        public required IImmutableList<string> Arguments { get; init; }

        public required IImmutableDictionary<string, string> Environment { get; init; } = ImmutableDictionary<string, string>.Empty;

        public required string? WorkingDirectory { get; init; }

        public required TimeSpan Timeout { get; init; }
    }
}