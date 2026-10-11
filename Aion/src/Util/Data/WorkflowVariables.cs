using System.Collections.Immutable;
using System.Diagnostics;
using Aion.Util.Buzz;
using Aion.Util.Data.Templating;

namespace Aion.Util.Data;

public class SchedulerVariableGroup : PropertyScriptObject, IVariableGroup
{
    public required string Name { get; init; } = null!;
    public required string Home { get; init; } = null!;
}

public class WorkflowVariableGroup : PropertyScriptObject, IVariableGroup
{
    public WorkflowVariableGroup(IImmutableDictionary<string, string> variables)
    {
        foreach (var (key, value) in variables)
        {
            Add(key, value);
        }
    }

    public required string Path { get; init; }
    public required WorkflowMode Mode { get; init; }
}

public class StepVariableGroup : PropertyScriptObject, IVariableGroup
{
    public string? Name { get; init; }
    public required int Index { get; init; }
    public ActivityTraceId TraceId => Activity.Current?.TraceId ?? default;
    public ActivitySpanId SpanId => Activity.Current?.SpanId ?? default;
    public string? ParentId => Activity.Current?.ParentId;
}