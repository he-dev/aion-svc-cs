using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Meta.Logging.Wiretap;
using Aion.Meta.Serilog;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Util.Buzz;

public class LoadLoggingPreset
(
    ILogger<LoadLoggingPreset> logger,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    // core: Simplifies other calls.
    public async Task<Serilog.ILogger> FromJsonOrDefault(string? loggingPresetNameOrPath, Serilog.ILogger defaultLogger)
    {
        if (loggingPresetNameOrPath is null)
        {
            logger.System.LogDebug("Logging preset name-or-path is null. Using the default logger.");
            return defaultLogger;
        }

        var loggingPresetPath = loggingPresetNameOrPath;

        if (!Path.IsPathRooted(loggingPresetNameOrPath))
        {
            logger.System.LogDebug("Logging preset is a name: '{logging.preset.path}'.", loggingPresetNameOrPath);
            loggingPresetPath = Path.Combine(schedulerOptions.Value.Home, "loggers", loggingPresetNameOrPath);
        }

        try
        {
            // core: Return the configuration.
            await using var fileStream = new FileStream(loggingPresetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var logging = await JsonSerializer.DeserializeAsync<JsonObject>(fileStream, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            });

            if (logging is null)
            {
                logger.System.LogError("Logging preset '{logging.preset.path}' does not contain valid logger JSON. Using default logger.", loggingPresetPath);
            }
            else
            {
                return logging.ToLogger();
            }
        }
        // meta: Single will throw this, so let's translate it to something meaningful.
        catch (Exception ex)
        {
            logger.System.LogError(ex, "Unable to deserialize '{logging.preset.path}'. Using default logger.", loggingPresetPath);
        }

        return defaultLogger;
    }
}