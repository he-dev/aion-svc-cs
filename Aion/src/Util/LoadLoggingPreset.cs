using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Data.Scheduler;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiretap.Core;

namespace Aion.Util;

public class LoadLoggingPreset
(
    ILogger<LoadLoggingPreset> logger,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    public async Task<global::Serilog.ILogger> FromFileOrNone(string? loggingPresetPath)
    {
        if (string.IsNullOrEmpty(loggingPresetPath))
        {
            logger.Baseline.LogDebug("Logging preset path is null or empty. Using the none-logger.");
            return global::Serilog.Core.Logger.None;
        }

        if (!Path.IsPathRooted(loggingPresetPath))
        {
            logger.Baseline.LogDebug("Logging preset path '{logging.preset.path}' is a name.", loggingPresetPath);
            loggingPresetPath = Path.Combine(schedulerOptions.Value.Home, "loggers", loggingPresetPath);
        }

        if (!File.Exists(loggingPresetPath))
        {
            logger.Baseline.LogError("Logging preset path '{logging.preset.path}' does not exist.", loggingPresetPath);
            return global::Serilog.Core.Logger.None;
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
                logger.Baseline.LogError("Logging preset '{logging.preset.path}' does not contain valid logger.", loggingPresetPath);
            }
            else
            {
                return logging.ToLogger();
            }
        }
        // meta: Single will throw this, so let's translate it to something meaningful.
        catch (Exception ex)
        {
            logger.Baseline.LogError(ex, "File '{logging.preset.path}' fails to deserialize as logging-preset. Falling back to default logger.", loggingPresetPath);
        }

        return global::Serilog.Core.Logger.None;
    }
}