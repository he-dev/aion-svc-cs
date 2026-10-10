using Quartz;

namespace Aion.Util.Data;

public record WorkflowPatch
{
    public ITrigger? Trigger { get; init; }
    public string[] Steps { get; init; } = [];

    public static WorkflowPatch Empty => new();
}