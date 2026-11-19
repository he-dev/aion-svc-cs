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

public abstract class Validate<TSubject>(ILogger<TSubject> logger, ForkLogging forkLogging)
{
    public bool Subject(TSubject subject)
    {
        var inMemoryFork = new InMemoryFork();
        using var unsubscribe = forkLogging.Into(inMemoryFork);

        foreach (var policy in Policies(logger))
        {
            try
            {
                policy.Check(subject, logger);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error checking the '{policy.name}' policy for '{policy.subject}'.", policy.Name, typeof(TSubject).Name);
            }
        }

        return inMemoryFork.AtLeast(LogLevel.Error).Any();
    }

    protected abstract IEnumerable<Policy> Policies(ILogger logger);

    protected abstract class Policy
    {
        public string Name => GetType().Name;

        public abstract void Check(TSubject subject, ILogger logger);
    }
}

public class ValidateWorkflowFile2(ILogger<WorkflowFile> logger, ForkLogging forkLogging) : Validate<WorkflowFile>(logger, forkLogging)
{
    protected override IEnumerable<Policy> Policies(ILogger logger)
    {
        yield return new Cron();
    }

    private class Cron : Policy
    {
        public override void Check(WorkflowFile subject, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(subject.Cron))
            {
                logger.LogError("Cannot use workflow property '{workflow.property}' because it is null or empty.", nameof(WorkflowFile.Cron));
            }
            else
            {
                var cronErrorMessage = (string?)null;
                try
                {
                    _ = CronExpression.Parse(subject.Cron);
                }
                catch (Exception e)
                {
                    cronErrorMessage = e.Message;
                }

                if (cronErrorMessage is not null)
                {
                    logger.LogError("Cannot use workflow property '{workflow.property}' because it is not a valid cron expression: {workflow.error}", nameof(WorkflowFile.Cron), cronErrorMessage);
                }
            }
        }
    }
}

public class ValidateWorkflowFile
(
    ILogger<ValidateWorkflowFile> logger,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    public bool That(WorkflowFile workflow)
    {
        using var about = logger.Baseline.BeginScopeAbout<ValidateWorkflowFile>();
        about.Zero.LogDebug(null);

        var isValid = true;

        if (string.IsNullOrWhiteSpace(workflow.Cron))
        {
            about.Tick.LogError("Cannot use workflow property '{workflow.property}' because it is null or empty.", nameof(WorkflowFile.Cron));
            isValid = false;
        }
        else
        {
            var cronErrorMessage = (string?)null;
            try
            {
                _ = CronExpression.Parse(workflow.Cron);
            }
            catch (Exception e)
            {
                cronErrorMessage = e.Message;
            }

            if (cronErrorMessage is not null)
            {
                about.Tick.LogError("Cannot use workflow property '{workflow.property}' because it is not a valid cron expression: {workflow.cron.error}", nameof(WorkflowFile.Cron), cronErrorMessage);
                isValid = false;
            }
        }

        if (workflow.Variables is { } variables)
        {
            foreach (var (key, value) in variables)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    about.Tick.LogError("Cannot use variable '{workflow.variable.name}' because it is null or empty.", key);
                    isValid = false;
                }
            }
        }

        if (workflow.Logger is not null)
        {
            if (!workflow.Logger.Equals("@profile", StringComparison.OrdinalIgnoreCase))
            {
                var loggersPath = Path.Join(schedulerOptions.Value.Home, "loggers", $"{workflow.Logger}.json");
                if (!File.Exists(loggersPath))
                {
                    about.Tick.LogError("Cannot use logger {workflow.logger.path} because it does not exist.", loggersPath);
                    isValid = false;
                }
            }
        }

        if (workflow.Steps is null)
        {
            about.Tick.LogError("Workflow issue: {workflow.property.name} property is null.", nameof(WorkflowFile.Steps));
            isValid = false;
        }
        else
        {
            if (workflow.Steps.Length == 0)
            {
                about.Tick.LogError("Workflow issue: {workflow.property.name} property is empty.", nameof(WorkflowFile.Steps));
                isValid = false;
            }
            else
            {
                if (workflow.Steps.Any(step => step is null))
                {
                    about.Tick.LogError("Workflow issue: one or more {workflow.property.name} is null or empty.", nameof(WorkflowFile.Steps));
                    isValid = false;
                }
                else
                {
                    foreach (var (step, index) in workflow.Steps.Select((x, i) => (x, i)))
                    {
                        // note: At this point steps are not null. C# just cannot figure that out.
                        if (!That(step!, index))
                        {
                            isValid = false;
                        }
                    }
                }
            }
        }

        about.Last.LogDebug(null);

        return isValid;
    }

    private bool That(WorkflowFile.Step step, int index)
    {
        using var about = logger.Baseline.BeginScopeAbout($"{nameof(ValidateWorkflowFile)}.Step");
        about.Zero.LogDebug(null);

        var isValid = true;

        if (step.Name is not null && string.IsNullOrWhiteSpace(step.Name))
        {
            about.Tick.LogError("Step {workflow.step.index} issue: {workflow.step.property.name} property is empty.", index, nameof(WorkflowFile.Step.Name));
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(step.FileName))
        {
            about.Tick.LogError("Step {workflow.step.index} issue: {workflow.step.property.name} property is empty.", index, nameof(WorkflowFile.Step.FileName));
            isValid = false;
        }

        if (step.Arguments is not null && step.Arguments.Any(string.IsNullOrWhiteSpace))
        {
            about.Tick.LogError("Step {workflow.step.index} issue: One or more arguments are null or empty.", index);
            isValid = false;
        }

        if (step.Environment is not null && step.Environment.Values.Any(string.IsNullOrWhiteSpace))
        {
            about.Tick.LogError("Step {workflow.step.index} issue: One or more environment variables are null or empty.", index);
            isValid = false;
        }

        if (step.WorkingDirectory is not null && !Directory.Exists(step.WorkingDirectory))
        {
            // mhmm?
        }

        if (step.Timeout is not null && step.Timeout < TimeSpan.Zero)
        {
            about.Tick.LogError("Step {workflow.step.index} issue: {workflow.step.property.name} is less than zero.", index, nameof(WorkflowFile.Step.Timeout));
            isValid = false;
        }

        about.Last.LogDebug(null);

        return isValid;
    }
}