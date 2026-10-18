using System;
using System.Collections.Generic;

namespace Aion.Util.Data;

// note: This class is deserialized from workflow JSON.
public record WorkflowSpecJson
{
    public Step?[]? Steps { get; init; }

    public bool? Disabled { get; init; }

    public string? Cron { get; init; }

    public Dictionary<string, string?>? Variables { get; init; }

    public string? Logger { get; init; }

    public record Step
    {
        public string? Name { get; init; }

        public bool? Disabled { get; init; }

        public string? FileName { get; init; }

        public string?[]? Arguments { get; init; }

        public Dictionary<string, string?>? Environment { get; init; }

        public string? WorkingDirectory { get; init; }

        public TimeSpan? Timeout { get; init; }
    }
}