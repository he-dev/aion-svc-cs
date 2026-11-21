using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Data.Specs;
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
    public async Task<bool> AsProcess(StepSpec stepSpec, WorkflowTriggerSource source, CancellationToken cancellationToken = default)
    {
        using var about = logger.Baseline.BeginScopeAbout<ExecuteStep>
        ([
            ("workflow.step.index", stepSpec.Index),
            ("workflow.step.name", stepSpec.Name)
        ]);

        try
        {
            about.Zero.LogDebug(null);

            var activity = Activity.Current!;
            var exitCode = await startProcess.Invoke
            (
                stepSpec.FileName,
                stepSpec.Arguments(source, activity.TraceId.ToString(), activity.ParentSpanId.ToString()).Join(" "),
                stepSpec.WorkingDirectory,
                stepSpec.Environment.ToDictionary(),
                stepSpec.Timeout,
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
            about.Last.LogError(ex, "Step {workflow.step.index} fails after {activity.duration_ms:N0} ms with an exception.");
            return false;
        }
    }
}