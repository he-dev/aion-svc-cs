using System;
using System.Collections.Generic;
using System.Linq;
using Aion.Util.Dotnet;

namespace Aion.Data.Configs;

public record WorkflowConfigDefault
{
    public Dictionary<string, string> Variables { get; init; } =  new();

    public string? Logger { get; init; }

    public static WorkflowConfigDefault Empty { get; } = new();

    public static WorkflowConfigDefault operator +(WorkflowConfigDefault left, WorkflowConfigDefault right)
    {
        return new WorkflowConfigDefault
        {
            Variables =
                left
                    .Variables
                    .Concat(right.Variables)
                    .LastValueWins(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(StringComparer.OrdinalIgnoreCase),
            Logger = right.Logger ?? left.Logger
        };
    }
}