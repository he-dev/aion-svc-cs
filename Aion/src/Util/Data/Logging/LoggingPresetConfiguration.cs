using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Aion.Util.Data.Logging;

public record LoggingPresetConfiguration
{
    public const string DefaultFileName = "logging-presets.json";

    // meta: The version of the logging-presets-file. Currently, not in use.
    public int Version { get; init; } = 1;

    public Dictionary<string, LoggingPreset> Presets { get; init; } = new();

    public static async Task<LoggingPresetConfiguration?> FromJson(string path)
    {
        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var data = await JsonSerializer.DeserializeAsync<LoggingPresetConfiguration>(fileStream, new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        });

        if(data is not null)
        {
            data = data with { Presets = data.Presets.ToDictionary(StringComparer.InvariantCultureIgnoreCase) };
        }

        return data;
    }
}

// ReSharper disable once ClassNeverInstantiated.Global
public record LoggingPreset
{
    public JsonObject Serilog { get; set; } = null!;
}