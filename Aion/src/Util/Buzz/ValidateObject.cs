using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Aion.Util.Data;

namespace Aion.Util.Buzz;

public static class ValidateObject
{
    public static IImmutableList<ValidationResult> That(WorkflowProfile profile, IServiceProvider services)
    {
        return new ValidationContext(profile).Evaluate(services.GetService);
    }

    public static IImmutableList<ValidationResult> That(WorkflowSpec spec, IServiceProvider services)
    {
        return new ValidationContext(spec).Evaluate(services.GetService);
    }

    public static IImmutableList<ValidationResult> That(WorkflowStepSpec spec)
    {
        return new ValidationContext(spec).Evaluate();
    }

    private static IImmutableList<ValidationResult> Evaluate(this ValidationContext context, Func<Type, object?>? getService = null)
    {
        if (getService is not null)
        {
            context.InitializeServiceProvider(getService);
        }

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true);
        return [.. results];
    }
}