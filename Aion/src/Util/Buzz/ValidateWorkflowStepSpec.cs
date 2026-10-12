using System;
using System.Collections.Generic;
using System.IO;
using Aion.Util.Data;

namespace Aion.Util.Buzz;

public class ValidateWorkflowStepSpec
{
    public IEnumerable<string> That(WorkflowStepSpec context)
    {
        if (context.Name is not null && string.IsNullOrWhiteSpace(context.Name))
        {
            yield return $"Cannot use [{nameof(WorkflowStepSpec)}.{nameof(context.Name)}] because it is empty.";
        }

        if (string.IsNullOrWhiteSpace(context.FileName))
        {
            yield return $"Cannot use [{nameof(WorkflowStepSpec)}.{nameof(context.FileName)}] because it is empty.";
        }

        if (context.WorkingDirectory is not null && !Directory.Exists(context.WorkingDirectory))
        {
            yield return $"Cannot use [{nameof(WorkflowStepSpec)}.{nameof(context.WorkingDirectory)}] that refers to '{context.WorkingDirectory}' because it does not exist.";
        }

        if (context.Timeout is not null && context.Timeout < TimeSpan.Zero)
        {
            yield return $"Cannot use [{nameof(WorkflowStepSpec)}.{nameof(context.Timeout)}] because it is less than zero.";
        }
    }
}