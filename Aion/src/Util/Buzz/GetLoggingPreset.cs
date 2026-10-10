using System.IO;
using System.Threading.Tasks;
using Aion.Meta.Serilog;
using Aion.Util.Data.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Core;

namespace Aion.Util.Buzz;

public class GetLoggingPreset
(
    ILogger<GetLoggingPreset> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    DeserializeLoggingPreset deserializeLoggingPreset
)
{
    public async Task<Serilog.ILogger> Where(string loggingPresetName)
    {
        if (string.IsNullOrWhiteSpace(loggingPresetName))
        {
            logger.LogInformation("Logging preset not specified.");
            return Logger.None;
        }

        // core: Create the path to the logging-preset-file.
        var loggingPresetPath = Path.Combine(schedulerOptions.Value.HomeDirectory.LoggersDirectory, $"{loggingPresetName}.json");
        var logging = await deserializeLoggingPreset.Where(loggingPresetPath);
        return logging is null ? Logger.None : logging.ToLogger();
    }
}