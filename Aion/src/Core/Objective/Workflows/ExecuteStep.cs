using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Meta;
using Aion.Meta.Logging.Wiretap;
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
        Workflow.Step.Okay step,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = logger.BeginActivity<ExecuteStep>
        (
            ("aion.workflow.step.index", step.Spec.Index),
            ("aion.workflow.step.name", step.Name)
        );

        try
        {
            activity.Core.Zero.LogInformation("Executing step {aion.workflow.step.index}.");
            var startProcessResult = await startProcess.Now
            (
                step.FileName,
                step.Arguments(Activity.Current!.TraceId.ToString()).Join(" "),
                step.WorkingDirectory,
                step.Environment.ToDictionary(),
                step.Timeout,
                cancellationToken
            );

            switch (startProcessResult.ExitCode)
            {
                case 0:
                {
                    activity.Core.Last.LogInformation("Step {aion.workflow.step.index} completed in {aion.activity.duration_ms:N0} ms.");
                    break;
                }
                default:
                {
                    using var exitScope = logger.BeginScopeWith(("aion.workflow.step.exit_code", startProcessResult.ExitCode));
                    activity.Core.Last.LogInformation("Step {aion.workflow.step.index} failed after {aion.activity.duration_ms:N0} ms with exit code {aion.workflow.step.exit_code}.");
                    break;
                }
            }

            return new ExecuteStepResult(step.Spec.Index)
            {
                Process = startProcessResult
            };
        }
        catch (OperationCanceledException)
        {
            activity.Core.Last.LogError("Step {aion.workflow.step.index} cancelled after {aion.activity.duration_ms:N0} ms.");
            // core: Let the workflow handle the cancellation.
            throw;
        }
        catch (Exception ex)
        {
            activity.Core.Last.LogError(ex, "Step {aion.workflow.step.index} failed after {aion.activity.duration_ms:N0} ms with an exception.");
            return new ExecuteStepResult(step.Spec.Index);
        }
    }
}

public record ExecuteStepResult(int StepIndex)
{
    public StartProcessResult? Process { get; init; }
}