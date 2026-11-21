using System;
using System.Collections.Immutable;
using Aion.Data.Templating;
using Quartz;

namespace Aion.Data.Specs;

public record WorkflowSpec
{
    public required WorkflowPath Path { get; init; }

    public required bool Enabled { get; init; }

    public required ITrigger Trigger { get; init; }

    public required IImmutableDictionary<string, TemplateString> Variables { get; init; }

    public required IImmutableList<StepSpec> Steps { get; init; }

    public required Serilog.ILogger Logger { get; init; } = Serilog.Core.Logger.None;
}

public record StepSpec
{
    public required int Index { get; init; }

    public required bool Enabled { get; init; }

    public string? Name { get; init; }

    public required TemplateString FileName { get; init; }

    public required IImmutableList<TemplateString> Arguments { get; init; }

    public required IImmutableDictionary<string, TemplateString> Environment { get; init; }

    public required string WorkingDirectory { get; init; } = string.Empty;

    public required TimeSpan Timeout { get; init; }
}