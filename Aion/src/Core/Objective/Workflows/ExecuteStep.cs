using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
    public async Task<ExecuteStepResult> Now
    (
        Workflow.Step step,
        CancellationToken cancellationToken = default
    )
    {
        // meta: Setup logging contexts.
        using var activity = new Activity(nameof(ExecuteStep)).Start();
        using var scope = logger.BeginScopeWith(("aion.workflow.step.index", step.Index), ("aion.workflow.step.name", step.Name));

        try
        {
            logger.LogInformation("Executing step {aion.workflow.step.index}.", step.Index);
            var startProcessResult = await startProcess.Now
            (
                step.FileName,
                step.Arguments().Join(" "),
                step.WorkingDirectory,
                step.Environment.ToDictionary(),
                step.Timeout,
                cancellationToken
            );

            switch (startProcessResult.ExitCode)
            {
                case 0:
                    activity.SetStatus(ActivityStatusCode.Ok).Stop();
                    logger.LogInformation("Step {aion.workflow.step.index} completed in {aion.workflow.step.duration_ms:N0} ms.", step.Index, activity.Duration);
                    break;
                default:
                    activity.SetStatus(ActivityStatusCode.Error).Stop();
                    logger.LogError("Step {aion.workflow.step.index} failed after {aion.workflow.step.duration_ms:N0} ms with exit code {aion.workflow.step.exit_code}.", step.Index, activity.Duration, startProcessResult.ExitCode);
                    break;
            }

            return new ExecuteStepResult(step.Index)
            {
                Process = startProcessResult
            };
        }
        catch (OperationCanceledException)
        {
            // core: Let the workflow handle the cancellation.
            throw;
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Step {aion.step.index} failed after {aion.step.duration_ms:N0} ms with an exception.", step.Index, activity.Duration);
            return new ExecuteStepResult(step.Index);
        }
    }
}

public record ExecuteStepResult(int StepIndex)
{
    public StartProcessResult? Process { get; init; }
}