using System.Collections.Immutable;

namespace Aion.Util.Data.StepExecutionRules;

public interface IStepExecutionRule
{
    bool Violated(Workflow.Step step, IImmutableList<int?> exitCodes);
}