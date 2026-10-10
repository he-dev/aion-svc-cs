using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Buzz;

public class DeserializeLoggingPreset(ILogger<DeserializeLoggingPreset> logger)
{
    public async Task<JsonObject?> Where(string loggingPresetPath)
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