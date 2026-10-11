using System;
using System.Collections.Immutable;
using System.Linq;
using Quartz;

namespace Aion.Util.Data;

public record Workflow
{
    public WorkflowPath Path { get; init; } = null!;

    // core: Make the user specify this value explicitly, so they don't activate workflows by accident.
    public bool Disabled { get; init; }

    public ITrigger Trigger { get; init; } = null!;

    public WorkflowMode Mode => Trigger is ICronTrigger ? WorkflowMode.Cron : WorkflowMode.User;

    public IImmutableDictionary<string, string> Variables { get; init; } = null!;

    public IImmutableList<Step> Steps { get; init; } = [];

    public Serilog.ILogger Logger { get; init; }

    public Exception? Exception { get; init; }

    public bool Okay => Exception is null && Steps.All(step => step.Exception is null);

    public record Step
    {
        public int Index { get; init; }

        public string? Name { get; init; }

        public bool Disabled { get; init; } = true;

        public string FileName { get; init; } = null!;

        // note: Arguments can pass runtime values such as activity ids, so this property must be lazy.
        public Func<string[]> Arguments { get; init; } = null!;

        public IImmutableDictionary<string, string> Environment { get; init; } = null!;

        public string WorkingDirectory { get; init; } = null!;

        public TimeSpan Timeout { get; init; }

        public Exception? Exception { get; init; }
    }
}