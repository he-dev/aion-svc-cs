using System;
using System.IO;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Util.Data.Scheduler;

public class SchedulerOptionsValidation : IValidateOptions<SchedulerOptions>
{
    public ValidateOptionsResult Validate(string? optionsName, SchedulerOptions options)
    {
        try
        {
            _ = CronExpression.Parse(options.Polling.Cron);
        }
        catch (Exception e)
        {
            return ValidateOptionsResult.Fail($"Invalid scheduler cron expression '{options.Polling.Cron}': {e.Message}");
        }

        if (!Path.Exists(options.Home))
        {
            return ValidateOptionsResult.Fail($"Scheduler home directory '{options.Home}' does not exist.");
        }

        return ValidateOptionsResult.Success;
    }
}