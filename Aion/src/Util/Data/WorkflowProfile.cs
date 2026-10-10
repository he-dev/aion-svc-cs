using System.Collections.Generic;
using System.Linq;

namespace Aion.Util.Data;



public class WorkflowProfile
{
    public Dictionary<string, string>? Variables { get; init; }
    public string? Logger { get; init; }

    public static WorkflowProfile operator +(WorkflowProfile left, WorkflowProfile right)
    {
        return new WorkflowProfile
        {
            Variables = (left.Variables ?? new()).Concat(right.Variables ?? new()).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Last().Value),
            Logger = right.Logger ?? left.Logger
        };
    }

    public static WorkflowProfile Empty { get; } = new();
}

