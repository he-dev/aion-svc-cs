using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Objective.Workflows;

public class ExecuteStep
(
    ILogger<ExecuteStep> logger,
    StartProcess startProcess
)
{
    public async Task<StepResult> Now(Workflow.Step step)
    {
        if (!step.Enabled)
        {
            return new StepResult { Index = step.Index };
        }

        // meta: Setup logging contexts.
        using var activity = new Activity(nameof(ExecuteStep)).Start();
        using var scope = logger.BeginScopeFrom(new { StepIndex = step.Index, StepName = step.Name });

        try
        {
            logger.LogInformation("Executing step: {StepIndex}", step.Index);
            var exitCode = await startProcess.Now
            (
                step.FileName,
                step.Timeout,
                psi =>
                {
                    psi.Arguments = step.Arguments().Join();
                    psi.WorkingDirectory = step.WorkingDirectory;
                    foreach (var (name, value) in step.Environment)
                    {
                        psi.Environment[name] = value;
                    }
                }
            );
            activity.SetStatus(ActivityStatusCode.Ok).Stop();

            switch (exitCode)
            {
                case 0: logger.LogInformation("Step {StepIndex} completed in {Duration:N0} ms.", step.Index, activity.Duration); break;
                default: logger.LogError("Step {StepIndex} failed after {Duration:N0} ms with exit code {ExitCode}. Next: '{OnError}'.", step.Index, activity.Duration, exitCode, step.OnError); break;
            }

            return new StepResult
            {
                Index = step.Index,
                ExitCode = exitCode,
                Duration = activity.Duration,
            };
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            if (ex is ProcessTimeout)
            {
                logger.LogWarning("Step {StepIndex} was cancelled after {Duration:N0} ms by timeout.", step.Index, activity.Duration);
            }
            else
            {
                logger.LogError(ex, "Step {StepIndex} failed after {Duration:N0} ms with an exception.", step.Index, activity.Duration);
            }

            return new StepResult
            {
                Index = step.Index,
                Exception = ex,
                Duration = activity.Duration,
            };
        }
    }
}

// meta: Makes testing easier.
public record StepResult
{
    public required int Index { get; init; }
    public int? ExitCode { get; init; }
    public Exception? Exception { get; init; }
    public TimeSpan Duration { get; init; }

    public StepStatus Status => Exception switch
    {
        ProcessTimeout => StepStatus.Timeout,
        _ => ExitCode switch
        {
            0 => StepStatus.Okay,
            null => StepStatus.None,
            _ => StepStatus.Error
        }
    };
}

public enum StepStatus
{
    None,
    Okay,
    Error,
    Timeout,
}