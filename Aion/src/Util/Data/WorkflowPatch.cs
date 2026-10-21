using Quartz;

namespace Aion.Util.Data;

// core: This class specifies properties that can be overridden by the user.
public record WorkflowPatch
{
    public bool Force { get; init; }
    public ITrigger? Trigger { get; init; }
    public string[] Steps { get; init; } = [];

    public static WorkflowPatch Empty => new();
}