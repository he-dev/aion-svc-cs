using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Quartz;

namespace Aion.Util.Data;

public static class WorkflowTemplateValidation
{
    public static WorkflowSpec Validate(WorkflowSpec spec)
    {
        var results =
            spec
                .Steps
                .Select((step, index) => (step, index))
                .Where(x => x.step.Enabled)
                .Aggregate(Validation.Evaluate(spec), (current, item) =>
                {
                    var items = new Dictionary<object, object?>
                    {
                        ["Parent"] = $"{nameof(WorkflowStepSpec)}[{item.index}]"
                    };
                    return current.AddRange(Validation.Evaluate(item.step, items));
                });

        return
            results.Any()
                ? throw new ValidationException(string.Join(" | ", results.Select(r => r.ErrorMessage)))
                : spec;
    }
}

public static class Validation
{
    public static IImmutableList<ValidationResult> Evaluate(object obj, IDictionary<object, object?>? items = null)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(obj, items: items);
        Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true);
        return results.ToImmutableList();
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public abstract class StringValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        var parent = ctx.Items.TryGetValue("Parent", out var p) && p is string s ? s : ctx.ObjectInstance.GetType().Name;
        var propertyType = ctx.ObjectType.GetProperty(ctx.MemberName!)?.PropertyType;
        if (propertyType != typeof(string))
        {
            return new ValidationResult($"{parent}.{ctx.MemberName} cannot be validated with {GetType().Name} as it applies only to string properties.", [ctx.MemberName!]);
        }

        return ValidateString(value as string, parent, ctx);
    }

    protected abstract ValidationResult? ValidateString(string? value, string parent, ValidationContext ctx);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotNullOrWhiteSpaceAttribute : StringValidationAttribute
{
    protected override ValidationResult? ValidateString(string? value, string parent, ValidationContext ctx)
    {
        return
            string.IsNullOrWhiteSpace(value)
                ? new ValidationResult($"{parent}.{ctx.MemberName} must not be null or white-space.", [ctx.MemberName!])
                : ValidationResult.Success;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MustBeCronAttribute : StringValidationAttribute
{
    protected override ValidationResult? ValidateString(string? value, string parent, ValidationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ValidationResult($"{parent}.{ctx.MemberName} must not be null or white-space.");
        }

        try
        {
            CronExpression.ValidateExpression(value);
        }
        catch (FormatException ex)
        {
            return new ValidationResult($"{parent}.{ctx.MemberName} is not a valid cron expression: {ex.Message}.", [ctx.MemberName!]);
        }

        return ValidationResult.Success;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SerilogOrPresetAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        var property = ctx.ObjectType.GetProperty(ctx.MemberName!)!;
        var parent = ctx.Items.TryGetValue("Parent", out var p) && p is string s ? s : ctx.ObjectInstance.GetType().Name;

        if (property.PropertyType != typeof(JsonObject))
        {
            return new ValidationResult($"{parent}.{ctx.MemberName} must be a JsonObject.", [ctx.MemberName!]);
        }

        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(property);

        if (value is null && nullabilityInfo.ReadState == NullabilityState.Nullable)
        {
            return ValidationResult.Success;
        }

        if (value is JsonObject template)
        {
            // core: Serilog must have a 'WriteTo' property.
            if (template.ContainsKey("WriteTo"))
            {
                return ValidationResult.Success;
            }

            // core: Preset must have a 'Preset' property.
            if (template.ContainsKey("Preset"))
            {
                return ValidationResult.Success;
            }
        }

        return new ValidationResult($"{parent}.{ctx.MemberName} must contain either a 'WriteTo' or a 'Preset' property.", [ctx.MemberName!]);
    }
}