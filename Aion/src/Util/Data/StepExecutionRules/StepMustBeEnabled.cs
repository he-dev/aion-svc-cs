using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Data.StepExecutionRules;

public class StepMustBeEnabled(ILogger<StepMustBeEnabled> logger) : IStepExecutionRule
{
    public bool Violated(Workflow.Step step, IImmutableList<int?> exitCodes)
    {
        if (!step.Enabled)
        {
            logger.LogWarning("Cannot execute this step because it is disabled.");
            return true;
        }

        return false;
    }
}