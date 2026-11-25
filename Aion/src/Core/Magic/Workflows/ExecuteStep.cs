using System;
using System.Collections.Immutable;
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
    public async Task<bool> AsProcess(StepSpec step, ImmutableList<IVariableGroup> variables, CancellationToken cancellationToken = default)
    {
        using var about = logger.Baseline.BeginScopeAbout<ExecuteStep>
        ([
            ("workflow.step.index", step.Index),
            ("workflow.step.name", step.Name)
        ]);
        about.Zero.LogDebug(null);

        variables =
        [
            .. variables, new StepVariableGroup
            {
                Index = step.Index,
                Name = step.Name
            }
        ];

        try
        {
            var exitCode = await startProcess.Invoke
            (
                step.FileName.Render(variables),
                step.Arguments.Select(arg => arg.Render(variables)).Join(" "),
                step.WorkingDirectory,
                step.Environment.ToDictionary(x => x.Key, x => x.Value.Render(variables)),
                step.Timeout,
                cancellationToken
            );

            using var exitCodeScope = logger.BeginScopeWith(("workflow.step.exit_code", exitCode));
            switch (exitCode)
            {
                case null:
                    about.Last.LogError("Step {workflow.step.index} does not complete.");
                    break;
                case 0:
                    about.Last.LogInformation("Step {workflow.step.index} completes in {activity.duration_ms:N0} ms with exit code {workflow.step.exit_code}.");
                    break;
                default:
                {
                    using var exitScope = logger.BeginScopeWith(("workflow.step.exit_code", exitCode));
                    about.Last.LogError("Step {workflow.step.index} fails after {activity.duration_ms:N0} ms with exit code {workflow.step.exit_code}.");
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