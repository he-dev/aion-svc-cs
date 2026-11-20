using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.Data;
using Aion.Data.Scheduler;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Wiretap.Core;

namespace Aion.Util;

public class ValidateWorkflowFile
(
    ILogger<ValidateWorkflowFile> logger,
    ForkLogging forkLogging,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    public bool Invoke(WorkflowFile workflow)
    {
        var inMemoryFork = new InMemoryFork();
        using var unsubscribe = forkLogging.Into(inMemoryFork);
        using var about = logger.Baseline.BeginScopeAbout<ValidateWorkflowFile>();
        about.Zero.LogDebug(null);

        var workflowProperty = new SelectProperty<WorkflowFile>();

        if (string.IsNullOrWhiteSpace(workflow.Cron))
        {
            about.Tick.LogError("{workflow.property}: {workflow.issue}", workflowProperty.Path(x => x.Cron), "null or empty");
        }
        else
        {
            try
            {
                _ = CronExpression.Parse(workflow.Cron);
            }
            catch (Exception e)
            {
                about.Tick.LogError("{workflow.property}: {workflow.issue}", workflowProperty.Path(x => x.Cron), e.Message);
            }
        }

        foreach (var (key, value) in workflow.Variables ?? new Dictionary<string, string?>())
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                about.Tick.LogError("{workflow.property}: {workflow.issue}", workflowProperty.Path(x => x.Variables![key]), "null or empty");
            }
        }

        if (workflow.Logger is not null)
        {
            var loggerPath = Path.Join(schedulerOptions.Value.Home, "loggers", $"{workflow.Logger}.json");
            if (!File.Exists(loggerPath))
            {
                about.Tick.LogError("{workflow.property}: {workflow.issue}", workflowProperty.Path(x => x.Logger), $"logger '{workflow.Logger}' does not exist");
            }
        }

        if (workflow.Steps is null || workflow.Steps.Length == 0)
        {
            about.Tick.LogError("{workflow.property}: {workflow.issue}", workflowProperty.Path(x => x.Steps), "there are no steps");
        }
        else
        {
            foreach (var (step, index) in workflow.Steps.Select((x, i) => (x, i)))
            {
                if (step is null)
                {
                    about.Tick.LogError("{workflow.property}: {workflow.issue}", workflowProperty.Path(x => x.Steps![index]), "step is null");
                }
                else
                {
                    // note: At this point steps are not null. C# just cannot figure that out.
                    Invoke(step!, index);
                }
            }
        }

        about.Last.LogDebug(null);
        return !inMemoryFork.AtLeast(LogLevel.Error).Any();
    }

    private void Invoke(WorkflowFile.Step step, int index)
    {
        using var about = logger.Baseline.BeginScopeAbout($"{nameof(ValidateWorkflowFile)}.Step");
        about.Zero.LogDebug(null);

        if (step.Name is not null && string.IsNullOrWhiteSpace(step.Name))
        {
            about.Tick.LogError("Step {workflow.step.index} issue: {workflow.step.property.name} property is empty.", index, nameof(WorkflowFile.Step.Name));
        }

        if (string.IsNullOrWhiteSpace(step.FileName))
        {
            about.Tick.LogError("Step {workflow.step.index} issue: {workflow.step.property.name} property is empty.", index, nameof(WorkflowFile.Step.FileName));
        }

        if (step.Arguments is not null && step.Arguments.Any(string.IsNullOrWhiteSpace))
        {
            about.Tick.LogError("Step {workflow.step.index} issue: One or more arguments are null or empty.", index);
        }

        if (step.Environment is not null && step.Environment.Values.Any(string.IsNullOrWhiteSpace))
        {
            about.Tick.LogError("Step {workflow.step.index} issue: One or more environment variables are null or empty.", index);
        }

        if (step.WorkingDirectory is not null && !Directory.Exists(step.WorkingDirectory))
        {
            // mhmm?
        }

        if (step.Timeout is not null && step.Timeout < TimeSpan.Zero)
        {
            about.Tick.LogError("Step {workflow.step.index} issue: {workflow.step.property.name} is less than zero.", index, nameof(WorkflowFile.Step.Timeout));
        }

        about.Last.LogDebug(null);
    }
}