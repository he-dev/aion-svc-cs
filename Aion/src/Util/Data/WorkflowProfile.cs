using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aion.Util.Data;

public class WorkflowProfile : IValidatableObject
{
    public Dictionary<string, string>? Variables { get; init; }

    public string? Logger { get; init; }

    public static WorkflowProfile operator +(WorkflowProfile left, WorkflowProfile right)
    {
        return new WorkflowProfile
        {
            Variables = (left.Variables ?? new()).Concat(right.Variables ?? new()).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Last().Value),
            Logger = right.Logger ?? left.Logger
        };
    }

    public static WorkflowProfile Dummy { get; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (this == Dummy)
        {
            yield return new  ValidationResult("The workflow profile failed to load.", [nameof(Variables)]);
            yield break;
        }

        foreach (var (key, value) in (Variables ?? new()))
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield return new ValidationResult($"Specify a valid variable value for '{key}'. Currently it's empty.", [nameof(Variables)]);
            }
        }

        if (Logger is not null)
        {
            var loggersPath = System.IO.Path.Join(context.GetRequiredService<IOptions<SchedulerOptions>>().Value.Home, "loggers", $"{Logger}.json");

            if (!System.IO.File.Exists(loggersPath))
            {
                yield return new ValidationResult($"The logger '{loggersPath}' does not exist.", [nameof(Logger)]);
            }
        }
    }
}