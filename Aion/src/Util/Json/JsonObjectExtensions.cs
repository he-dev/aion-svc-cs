using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace Aion.Util.Json;

public static class JsonObjectExtensions
{
    [return: NotNullIfNotNull(nameof(source))]
    public static JsonObject? Clone(this JsonObject? source)
    {
        return source is null ? null : JsonNode.Parse(source.ToJsonString())!.AsObject();
    }
}