using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Data.StepExecutionRules;

public class StepDependsOnPrevious(ILogger<StepDependsOnPrevious> logger) : IStepExecutionRule
{
    private static readonly Regex IntArrayRegex = new(@"^\[(-?\d+(?:,-?\d+)*)?\]$", RegexOptions.Compiled);

    public bool Violated(Workflow.Step step, IImmutableList<int?> exitCodes)
    {
        // note: Currently, there is only one DependsOn rule: "$previous".
        // core: This check is irrelevant for the first step, so ignore it.
        if (step.Index > 0 && step is { OnError: not null })
        {
            if (step is { OnError: "$previous" } && exitCodes.Last() is not 0)
            {
                logger.LogWarning("Cannot execute this step because it depends on the previous one and it failed.");
                return true;
            }

            if (TryParseIntArray(step.OnError, out var indices) && indices.Any(i => exitCodes[i] is not 0))
            {
                logger.LogWarning("Cannot execute this step because it depends on [{DependsOn}] and one of them failed.", indices);
                return true;
            }
        }

        return false;
    }

    public static bool TryParseIntArray(string value, [MaybeNullWhen(false)] out ImmutableList<int> result)
    {
        value = value.Replace(" ", string.Empty);

        if (IntArrayRegex.Matches(value) is { Count: > 0 } matches)
        {
            result = matches.Select(m => int.Parse(m.Value)).ToImmutableList();
            return true;
        }

        result = null;
        return false;
    }
}