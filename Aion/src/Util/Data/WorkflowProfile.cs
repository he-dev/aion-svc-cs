using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Aion.Meta;

namespace Aion.Util.Data;

public record WorkflowProfile
{
    public record Okay : WorkflowProfile
    {
        public IImmutableDictionary<string, string> Variables { get; init; } = ImmutableDictionary<string, string>.Empty;

        public string? Logger { get; init; }

        public static Okay operator +(Okay left, Okay right)
        {
            return new Okay
            {
                Variables = left.Variables.Concat(right.Variables).LastValueWins().ToImmutableDictionary(),
                Logger = right.Logger ?? left.Logger
            };
        }
    }

    public record Yuck : WorkflowProfile
    {
        public required string Path { get; init; }
        public required IImmutableList<string> Issues { get; init; }
    }
}