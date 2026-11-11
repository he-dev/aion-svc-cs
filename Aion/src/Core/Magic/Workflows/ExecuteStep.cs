using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Util;
using Aion.Util.Dotnet;
using Microsoft.Extensions.Logging;
using Wiretap.Core;

namespace Aion.Core.Magic.Workflows;

public class ExecuteStep
(
    ILogger<ExecuteStep> logger,
    StartProcess startProcess
)
{
    public async Task<bool> AsProcess(Workflow.Step step, CancellationToken cancellationToken = default)
    {
        using var about = logger.Baseline.BeginScopeAbout<ExecuteStep>
        ([
            ("workflow.step.index", step.Spec.Index),
            ("workflow.step.name", step.Name)
        ]);

        try
        {
            about.Zero.LogDebug(null);

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
                    about.Last.LogError("Step {workflow.step.index} did not complete.");
                    break;
                case 0:
                    about.Last.LogInformation("Step {workflow.step.index} completed in {activity.duration_ms:N0} ms.");
                    break;
                default:
                {
                    using var exitScope = logger.BeginScopeWith(("workflow.step.exit_code", exitCode));
                    about.Last.LogError("Step {workflow.step.index} failed after {activity.duration_ms:N0} ms with exit code {workflow.step.exit_code}.");
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
            about.Last.LogError(ex, "Step {workflow.step.index} failed after {activity.duration_ms:N0} ms with an exception.");
            return false;
        }
    }
}