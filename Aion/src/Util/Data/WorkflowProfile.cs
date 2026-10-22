using System.Collections.Immutable;
using System.Linq;
using Aion.Meta;

namespace Aion.Util.Data;

public record WorkflowProfile
{
    public IImmutableDictionary<string, string> Variables { get; init; } = ImmutableDictionary<string, string>.Empty;

    public string? Logger { get; init; }

    public static WorkflowProfile Empty { get; } = new();

    public static WorkflowProfile operator +(WorkflowProfile left, WorkflowProfile right)
    {
        return new WorkflowProfile
        {
            Variables = left.Variables.Concat(right.Variables).LastValueWins().ToImmutableDictionary(),
            Logger = right.Logger ?? left.Logger
        };
    }
}