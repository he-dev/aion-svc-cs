using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Aion.Util.Data.Logging;

public class LoggingPresetExpression
{
    public LoggingPresetExpression(string value)
    {
        if (Regex.Match(value, @"^((?<file>[a-z0-9\-_]+\.json):)?(?<preset>[a-z0-9\-_]+)$", RegexOptions.IgnoreCase) is { Success: true } match)
        {
            if (match.Groups["file"] is { Success: true } file)
            {
                File = file.Value;
            }

            Name = match.Groups["preset"].Value;
        }
        else
        {
            throw new ArgumentException($"The code is trying to create a {nameof(LoggingPresetExpression)} from '{value}' but it does not match the required format: file_name.json[:preset_name]", nameof(value));
        }
    }

    public string File { get; init; } = LoggingPresetConfiguration.DefaultFileName;

    public string Name { get; init; }
}

public class LoggingPresetExpressionConverter : JsonConverter<LoggingPresetExpression>
{
    public override LoggingPresetExpression? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString() is { } value ? new LoggingPresetExpression(value) : null;
    }

    public override void Write(Utf8JsonWriter writer, LoggingPresetExpression value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}