using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aion.Util.Data;

[JsonConverter(typeof(StepIdentifierConverter))]
public record StepIdentifier(string Value) : IEquatable<int>, IEquatable<string>
{
    public bool Equals(int other) => Value.Equals(other.ToString());
    public bool Equals(string? other) => Value.Equals(other, StringComparison.OrdinalIgnoreCase);

    public static bool operator ==(StepIdentifier left, int right) => left.Equals(right);
    public static bool operator !=(StepIdentifier left, int right) => !(left == right);

    public static bool operator ==(StepIdentifier left, string? right) => left.Equals(right);
    public static bool operator !=(StepIdentifier left, string? right) => !(left == right);
}

public class StepIdentifierConverter : JsonConverter<StepIdentifier>
{
    public override StepIdentifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
            {
                var index = reader.GetInt32();
                return index < 0
                    ? throw new JsonException("Step index must be greater or equal zero.")
                    : new StepIdentifier(index.ToString());
            }
            case JsonTokenType.String:
            {
                var name = reader.GetString();
                return
                    string.IsNullOrEmpty(name)
                        ? throw new JsonException("Step name must not be null or empty.")
                        : new StepIdentifier(name);
            }
            default:
                throw new JsonException("Expected step index or name.");
        }
    }

    public override void Write(Utf8JsonWriter writer, StepIdentifier value, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }
}