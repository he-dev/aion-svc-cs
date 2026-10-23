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
using Activity = System.Diagnostics.Activity;

namespace Aion.Core.Objective.Workflows;

public class ExecuteStep
(
    ILogger<ExecuteStep> logger,
    StartProcess startProcess
)
{
    public async Task<bool> AsProcess(Workflow.Step step, CancellationToken cancellationToken = default)
    {
        using var activity = logger.BeginScopeAbout<ExecuteStep>
        ([
            ("workflow.step.index", step.Spec.Index),
            ("workflow.step.name", step.Name)
        ]);

        try
        {
            activity.System.Ready.LogInformation("Executing step {workflow.step.index}.");
            var exitCode = await startProcess.Invoke
            (
                step.FileName,
                step.Arguments(Activity.Current!.TraceId.ToString()).Join(" "),
                step.WorkingDirectory,
                step.Environment.ToDictionary(),
                step.Timeout,
                cancellationToken
            );

            switch (exitCode)
            {
                case null:
                    activity.Domain.Limbo.LogInformation("Step {workflow.step.index} did not complete.");
                    break;
                case 0:
                    activity.Domain.Limbo.LogInformation("Step {workflow.step.index} completed in {activity.duration_ms:N0} ms.");
                    break;
                default:
                {
                    using var exitScope = logger.BeginScopeWith(("workflow.step.exit_code", exitCode));
                    activity.Domain.Limbo.LogInformation("Step {workflow.step.index} failed after {activity.duration_ms:N0} ms with exit code {workflow.step.exit_code}.");
                }
                    break;
            }

            return exitCode == 0;
        }
        catch (OperationCanceledException)
        {
            // core: Let the workflow handle the cancellation.
            throw;
        }
        catch (Exception ex)
        {
            activity.Domain.Limbo.LogError(ex, "Step {workflow.step.index} failed after {activity.duration_ms:N0} ms with an exception.");
            return false;
        }
    }
}