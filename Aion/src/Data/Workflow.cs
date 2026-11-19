using System;
using System.Collections.Immutable;
using Quartz;

namespace Aion.Data;

public delegate IImmutableList<string> RenderArguments(WorkflowTriggerSource source, string traceId, string parentSpanId);

public record Workflow
{
    public required WorkflowSpec Spec { get; init; }

    public required bool Enabled { get; init; }

    // core: Use a trigger that is always in the past to satisfy the null-object pattern and keep it harmless.
    public required ITrigger Trigger { get; init; }

    public required IImmutableDictionary<string, string> Variables { get; init; }

    public required IImmutableList<Step> Steps { get; init; }

    public required Serilog.ILogger Logger { get; init; } = Serilog.Core.Logger.None;

    public record Step
    {
        public required WorkflowSpec.Step Spec { get; init; }

        public required int Index { get; init; }

        public required bool Enabled { get; init; }

        public string? Name { get; init; }

        public required string FileName { get; init; }

        // note: Arguments can pass runtime values such as activity ids, so this property must be lazy.
        public required RenderArguments Arguments { get; init; }

        public required IImmutableDictionary<string, string> Environment { get; init; }

        public required string WorkingDirectory { get; init; }

        public required TimeSpan Timeout { get; init; }
    }
}