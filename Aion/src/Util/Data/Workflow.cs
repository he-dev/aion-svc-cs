using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Quartz;

namespace Aion.Util.Data;

public abstract record Workflow
{
    public record Okay : Workflow
    {
        public required WorkflowSpec.Okay Spec { get; init; }

        public required bool Enabled { get; init; }

        // core: Use a trigger that is always in the past to satisfy the null-object pattern and keep it harmless.
        public required ITrigger Trigger { get; init; }

        public WorkflowMode Mode => Trigger is ICronTrigger ? WorkflowMode.Cron : WorkflowMode.User;

        public required IImmutableDictionary<string, string> Variables { get; init; }

        public required IImmutableList<Step> Steps { get; init; }

        public required Serilog.ILogger Logger { get; init; } = Serilog.Core.Logger.None;
    }

    public record Yuck : Workflow
    {
        public required WorkflowSpec.Yuck Spec { get; init; }
    }

    public abstract record Step
    {
        public required WorkflowSpec.Step Spec { get; init; }

        public required int Index { get; init; }

        public record Okay : Step
        {
            public required bool Enabled { get; init; }

            public string? Name { get; init; }

            public required string FileName { get; init; }

            // note: Arguments can pass runtime values such as activity ids, so this property must be lazy.
            public required Func<string, IEnumerable<string>> Arguments { get; init; }

            public required IImmutableDictionary<string, string> Environment { get; init; }

            public required string WorkingDirectory { get; init; }

            public required TimeSpan Timeout { get; init; }
        }

        public record Yuck : Step
        {
            public required IImmutableList<string> Issues { get; init; }
        }
    }
}