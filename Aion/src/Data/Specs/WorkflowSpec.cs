using System;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Data.Templating;
using Aion.Util;

namespace Aion.Data.Specs;

public delegate Task<T> Render<T>(ImmutableList<IVariableGroup> variables);

public record WorkflowSpec
{
    public required WorkflowPath Path { get; init; }

    public required bool Enabled { get; init; }

    public required string Cron { get; init; }

    public required IImmutableDictionary<string, TemplateString> Variables { get; init; }

    public required IImmutableList<StepSpec> Steps { get; init; }

    public required Render<JsonObject?> Logger { get; init; }
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