using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.Data.Configs;
using Aion.Data.Scheduler;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Wiretap.Core;

namespace Aion.Util;

public class ValidateWorkflowConfig
(
    ILogger<ValidateWorkflowConfig> logger,
    ForkLogging forkLogging,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    private static readonly SelectProperty<WorkflowConfig> WorkflowProperty = new();

    public bool Invoke(WorkflowConfig workflow)
    {
        var inMemoryFork = new InMemoryFork();
        using var unsubscribe = forkLogging.Into(inMemoryFork);
        using var about = logger.Baseline.BeginScopeAbout<ValidateWorkflowConfig>();
        about.Zero.LogDebug(null);


        if (string.IsNullOrWhiteSpace(workflow.Cron))
        {
            about.Tick.LogError("{workflow.property}: {workflow.issue}", WorkflowProperty.Path(x => x.Cron), "null or empty");
        }
        else
        {
            try
            {
                _ = CronExpression.Parse(workflow.Cron);
            }
            catch (Exception e)
            {
                about.Tick.LogError("{workflow.property}: {workflow.issue}", WorkflowProperty.Path(x => x.Cron), e.Message);
            }
        }

        foreach (var (key, value) in workflow.Variables ?? new Dictionary<string, string?>())
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                about.Tick.LogError("{workflow.property}: {workflow.issue}", WorkflowProperty.Path(x => x.Variables![key]), "null or empty");
            }
        }

        if (workflow.Logger is not null)
        {
            var loggerPath = Path.Join(schedulerOptions.Value.Home, "loggers", $"{workflow.Logger}.json");
            if (!File.Exists(loggerPath))
            {
                about.Tick.LogError("{workflow.property}: {workflow.issue}", WorkflowProperty.Path(x => x.Logger), $"logger '{workflow.Logger}' does not exist");
            }
        }

        if (workflow.Steps is null || workflow.Steps.Length == 0)
        {
            about.Tick.LogError("{workflow.property}: {workflow.issue}", WorkflowProperty.Path(x => x.Steps), "there are no steps");
        }
        else
        {
            foreach (var (step, index) in workflow.Steps.Select((x, i) => (x, i)))
            {
                if (step is null)
                {
                    about.Tick.LogError("{workflow.property}: {workflow.issue}", WorkflowProperty.Path(x => x.Steps![index]), "is null");
                }
                else
                {
                    Invoke(step, index);
                }
            }
        }

        about.Last.LogDebug(null);
        return !inMemoryFork.AtLeast(LogLevel.Error).Any();
    }

    private void Invoke(StepConfig step, int index)
    {
        using var about = logger.Baseline.BeginScopeAbout($"{nameof(ValidateWorkflowConfig)}.Step");
        about.Zero.LogDebug(null);

        if (step.Name is not null && string.IsNullOrWhiteSpace(step.Name))
        {
            about.Tick.LogError("{workflow.property}: {workflow.issue}", WorkflowProperty.Path(x => x.Steps![index]!.Name), "is empty");
        }

        if (string.IsNullOrWhiteSpace(step.FileName))
        {
            about.Tick.LogError("{workflow.property}: {workflow.issue}", WorkflowProperty.Path(x => x.Steps![index]!.FileName), "is null or empty");
        }


        foreach (var (argument, i) in (step.Arguments ?? []).Select((x, i) => (x, i)))
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                about.Tick.LogError("{workflow.property}: {workflow.issue}", WorkflowProperty.Path(x => x.Steps![index]!.Arguments![i]), "is null or empty");
            }
        }

        foreach (var (key, value) in step.Environment ?? new Dictionary<string, string?>())
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                about.Tick.LogError("{workflow.property}: {workflow.issue}", WorkflowProperty.Path(x => x.Steps![index]!.Environment![key]), "is null or empty");
            }
        }

        if (step.WorkingDirectory is not null && !Directory.Exists(step.WorkingDirectory))
        {
            // mhmm?
        }

        if (step.Timeout < TimeSpan.Zero)
        {
            about.Tick.LogError("{workflow.property}: {workflow.issue}", WorkflowProperty.Path(x => x.Steps![index]!.Timeout), "is less than zero");
        }

        about.Last.LogDebug(null);
    }
}