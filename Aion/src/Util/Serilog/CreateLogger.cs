using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Wiretap.Core;

namespace Aion.Util.Serilog;

public class CreateLogger(ILogger<CreateLogger> logger)
{
    // core: Converts a Serilog configuration into a Serilog logger.
    // note: This is a bit tricky, but it's the only way to get the logger to work.
    public global::Serilog.ILogger? FromJsonOrDefault(JsonObject? serilog)
    {
        try
        {
            // meta: This node is required in the appsettings.json for Serilog to find its settings.
            serilog = new JsonObject { ["Serilog"] = serilog };

            // meta: Uses an in-memory appsettings.json from which Serilog can create its sinks.
            var json = serilog.ToJsonString();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var config = new ConfigurationBuilder().AddJsonStream(stream).Build();
            return new LoggerConfiguration().ReadFrom.Configuration(config).CreateLogger();
        }
        catch (Exception e)
        {
            logger.Internal.LogError(e, "JsonObject fails to turn into a logger.");
        }

        return null;
    }
}