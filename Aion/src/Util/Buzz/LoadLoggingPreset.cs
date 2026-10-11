using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Meta.Serilog;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Util.Buzz;

public class LoadLoggingPreset(ILogger<LoadLoggingPreset> logger, IOptions<SchedulerOptions> schedulerOptions)
{
    public async Task<Serilog.ILogger> Where(string loggingPresetName)
    {
        if (string.IsNullOrWhiteSpace(loggingPresetName))
        {
            logger.LogInformation("Logging preset not specified.");
            return Serilog.Core.Logger.None;
        }

        // core: Create the path to the logging-preset-file.
        var loggingPresetPath = Path.Combine(schedulerOptions.Value.Home, "loggers", $"{loggingPresetName}.json");
        var logging = await Deserialize(loggingPresetPath);
        return logging is null ? Serilog.Core.Logger.None : logging.ToLogger();
    }

    public async Task<JsonObject?> Deserialize(string loggingPresetPath)
    {
        if (!File.Exists(loggingPresetPath))
        {
            logger.LogError("Logging preset '{LoggingPresetPath}' does not exist.", loggingPresetPath);
            return null;
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
                logger.LogError("Logging preset '{LoggingPresetPath}' does not contain valid logger JSON.", loggingPresetPath);
            }

            return logging;
        }
        // meta: Single will throw this, so let's translate it to something meaningful.
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to deserialize '{LoggingPresetPath}'.", loggingPresetPath);
            return null;
        }
    }
}