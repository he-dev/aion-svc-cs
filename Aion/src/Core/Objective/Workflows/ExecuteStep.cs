using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Meta;
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
        if (step.Disabled)
        {
            return new StepResult { Index = step.Index };
        }

        // meta: Setup logging contexts.
        using var activity = new Activity(nameof(ExecuteStep)).Start();
        using var scope = logger.BeginScopeWith(("aion.step.index", step.Index), ("aion.step.name", step.Name));

        try
        {
            logger.LogInformation("Executing step {aion.step.index} [{aion.step.name}]", step.Index, step.Name);
            var exitCode = await startProcess.Now
            (
                step.FileName,
                step.Timeout,
                psi =>
                {
                    psi.Arguments = step.Arguments().Join(" ");
                    psi.WorkingDirectory = step.WorkingDirectory;
                    foreach (var (name, value) in step.Environment)
                    {
                        psi.Environment[name] = value;
                    }
                }
            );

            switch (exitCode)
            {
                case 0:
                    activity.SetStatus(ActivityStatusCode.Ok).Stop();
                    logger.LogInformation("Step {aion.step.index} [{aion.step.name}] completed in {aion.step.duration_ms:N0} ms.", step.Index, step.Name, activity.Duration);
                    break;
                default:
                    activity.SetStatus(ActivityStatusCode.Error).Stop();
                    logger.LogError("Step {aion.step.index} [{aion.step.name}] failed after {aion.step.duration_ms:N0} ms with exit code {aion.step.exit_code}.", step.Index, step.Name, activity.Duration, exitCode);
                    break;
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
                logger.LogWarning("Step {aion.step.index} [{aion.step.name}] was cancelled after {aion.step.duration_ms:N0} ms by timeout.", step.Index, step.Name, activity.Duration);
            }
            else
            {
                logger.LogError(ex, "Step {aion.step.index} [{aion.step.name}] failed after {aion.step.duration_ms:N0} ms with an exception.", step.Index, step.Name, activity.Duration);
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
        not null => StepStatus.Fail,
        _ => ExitCode switch
        {
            0 => StepStatus.Okay,
            null => StepStatus.None,
            _ => StepStatus.Fail
        }
    };
}

public enum StepStatus
{
    None,
    Okay,
    Fail,
    Timeout,
}