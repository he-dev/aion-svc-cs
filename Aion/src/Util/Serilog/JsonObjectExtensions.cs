using System;
using System.Text.Json.Nodes;
using Aion.Util.Json;

namespace Aion.Util.Serilog;

public static class JsonObjectExtensions
{
    // core: Allows modifying each path property it finds on the "File" sink".
    public static JsonObject? ForEachPath(this JsonObject? serilog, Func<string, string> modifyPath)
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