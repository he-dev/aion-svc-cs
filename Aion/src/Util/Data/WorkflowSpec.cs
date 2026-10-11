using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json.Serialization;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Util.Data;

// note: This class is deserialized from workflow JSON.
public record WorkflowSpec : IValidatableObject
{
    public bool Disabled { get; init; }

    public string Cron { get; init; } = null!;

    public Dictionary<string, string>? Variables { get; init; }

    public string? Logger { get; init; }

    public WorkflowStepSpec[] Steps { get; init; } = null!;

    [JsonIgnore]
    public WorkflowPath Path { get; init; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(Cron))
        {
            yield return new ValidationResult("Specify a valid cron expression. Currently it's empty.", [nameof(Cron)]);
        }

        var cronErrorMessage = (string?)null;
        try
        {
            _ = CronExpression.Parse(Cron);
        }
        catch (Exception e)
        {
            cronErrorMessage = e.Message;
        }

        if (cronErrorMessage is not null)
        {
            yield return new ValidationResult($"Specify a valid cron expression. Currently it'S invalid: {cronErrorMessage}", [nameof(Cron)]);
        }

        foreach (var (key, value) in (Variables ?? new()))
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield return new ValidationResult($"Specify a valid variable value for '{key}'. Currently it's empty.", [nameof(Variables)]);
            }
        }

        if (Steps is not { Length: > 0 })
        {
            yield return new ValidationResult("Specify at least one step.", [nameof(Steps)]);
        }

        if (Logger is not null)
        {
            var loggersPath = System.IO.Path.Join(context.GetRequiredService<IOptions<SchedulerOptions>>().Value.Home, "loggers", $"{Logger}.json");
            if (!System.IO.File.Exists(loggersPath))
            {
                yield return new ValidationResult($"The logger '{Logger}' does not exist.", [nameof(Logger)]);
            }
        }
    }
}

public record WorkflowStepSpec : IValidatableObject
{
    [JsonIgnore]
    public int Index { get; init; } = -1;

    public string? Name { get; init; }

    public bool Disabled { get; init; } = true;

    public string FileName { get; init; } = null!;

    public string[]? Arguments { get; init; }

    public Dictionary<string, string>? Environment { get; init; }

    public string? WorkingDirectory { get; init; }

    public TimeSpan? Timeout { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Name is not null && string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("Specify a non-blank step name or leave it null.", [nameof(Name)]);
        }

        if (string.IsNullOrWhiteSpace(FileName))
        {
            yield return new ValidationResult("Specify a non-blank file name.", [nameof(FileName)]);
        }

        if (WorkingDirectory is not null && !Directory.Exists(WorkingDirectory))
        {
            yield return new ValidationResult("Specify an existing working directory because '{WorkingDirectory}' does not exist'.", [nameof(WorkingDirectory)]);
        }

        if (Timeout is not null && Timeout < TimeSpan.Zero)
        {
            yield return new ValidationResult("Specify a timeout that is greater than zero.", [nameof(Timeout)]);
        }
    }
}