using System;

namespace Aion.Data;

// core: This class specifies properties that can be overridden by the user.
public record WorkflowOverride
{
    public DateTimeOffset StartAt { get; init; }
    public string[] Steps { get; init; } = [];
}