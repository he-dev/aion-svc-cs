using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;

namespace Aion.Util.Serilog;

public static class LoggerExtensions
{
    // util: Wraps Serilog in Microsoft's ILoggerFactory, so it get be retrieved as Microsoft's ILogger.
    public static ILoggerFactory ToLoggerFactory(this global::Serilog.ILogger? logger)
    {
        // util: Wraps the Serilog's logger into Microsoft's logger.
        return
            logger is null
                ? NullLoggerFactory.Instance
                : LoggerFactory.Create(builder => { builder.ClearProviders().AddSerilog(logger, dispose: true); });
    }
}