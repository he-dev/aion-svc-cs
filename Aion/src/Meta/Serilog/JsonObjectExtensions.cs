using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using Aion.Meta.Json;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;

namespace Aion.Meta.Serilog;

public static class JsonObjectExtensions
{
    // core: Converts a Serilog configuration into a Serilog logger.
    // note: This is a bit tricky, but it's the only way to get the logger to work.
    public static ILogger ToLogger(this JsonObject serilog)
    {
        //if (serilog is null) return Logger.None;

        // meta: This node is required in the appsettings.json for Serilog to find its settings.
        serilog = new JsonObject { ["Serilog"] = serilog };

        // meta: Uses an in-memory appsettings.json from which Serilog can create its sinks.
        var json = serilog.ToJsonString();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var config = new ConfigurationBuilder().AddJsonStream(stream).Build();
        return new LoggerConfiguration().ReadFrom.Configuration(config).CreateLogger();
    }

    // core: Allows modifying each path property it finds on the "File" sink".
    public static JsonObject? WithPaths(this JsonObject? serilog, Func<string, string> modifyPath)
    {
        if (serilog is null) return null;

        // core: Scan sinks for the "path" property and run it through the template engine.
        if (serilog["WriteTo"] is JsonArray writeTo)
        {
            // meta: It needs to be cloned, otherwise the original object will be modified, and we do not want that.
            serilog = serilog.Clone();

            foreach (var sink in writeTo)
            {
                // meta: Make sure the path property really exists.
                if (sink is not null && sink["Name"]?.GetValue<string>() == "File" && sink["Args"] is JsonObject args && args["path"] is JsonValue path)
                {
                    var template = path.GetValue<string>();
                    args["path"] = modifyPath(template);
                }
            }
        }

        return serilog;
    }
}