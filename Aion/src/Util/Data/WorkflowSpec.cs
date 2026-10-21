using System;
using System.Collections.Immutable;
using System.Linq;
using Aion.Meta;

namespace Aion.Util.Data;

public record WorkflowSpec
{
    public required WorkflowPath Path { get; init; }

    public bool Disabled { get; init; }

    public required string Cron { get; init; }

    public IImmutableDictionary<string, string> Variables { get; init; } = ImmutableDictionary<string, string>.Empty;

    public string? Logger { get; init; }

    public required IImmutableList<Step> Steps { get; init; }

    public record Step
    {
        public required int Index { get; init; }

        public string? Name { get; init; }

        public bool Disabled { get; init; }

        public required string FileName { get; init; }

        public required IImmutableList<string> Arguments { get; init; }

        public required IImmutableDictionary<string, string> Environment { get; init; } = ImmutableDictionary<string, string>.Empty;

        public required string? WorkingDirectory { get; init; }

        public required TimeSpan Timeout { get; init; }
    }

    public static WorkflowSpec operator +(WorkflowSpec spec, WorkflowProfile profile)
    {
        return spec with
        {
            Variables = profile.Variables.Concat(spec.Variables).LastValueWins().ToImmutableDictionary(),
            Logger =
            // core: Switch to the profile logger if the file logger is "@profile".
            spec.Logger?.Equals("@profile", StringComparison.OrdinalIgnoreCase) == true
                ? profile.Logger
                // core: Use own logger, or the profile's if own one is not specified.
                : spec.Logger ?? profile.Logger,
        };
    }
}