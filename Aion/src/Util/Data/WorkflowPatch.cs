using System;

namespace Aion.Util.Data;

// core: This class specifies properties that can be overridden by the user.
public record WorkflowPatch
{
    public DateTimeOffset StartAt { get; init; }
    public string[] Steps { get; init; } = [];
}