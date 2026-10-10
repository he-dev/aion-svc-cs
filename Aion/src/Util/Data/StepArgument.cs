using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aion.Meta;
using Aion.Util.Data.Templating.Services;

namespace Aion.Util.Data;

public record StepArgument(string Name, string[]? Values)
{
    private static readonly Regex SuffixPattern = new(@"(?<NameValueSeparator>[:=])?(?<ItemList>\[(?<ItemSeparator>[,;])?\])?$", RegexOptions.Compiled);

    private const string DefaultArgumentSeparator = " ";
    private const string DefaultNameValueSeparator = " ";
    private const string DefaultItemSeparator = " ";

    public static class Names
    {
        public const string Passthrough = "$";
        public const string Positional = "_";
    }

    public record Positional(params string[] Values) : StepArgument(Names.Positional, Values);

    public override string ToString()
    {
        switch (Name.Trim())
        {
            // Passthrough: $ -> single raw string, no processing
            case Names.Passthrough:
                return
                    Values is { Length: 1 }
                        ? Values[0]
                        : throw new InvalidOperationException("Raw passthrough '$' requires exactly one value.");
            // Positional: _ -> just values
            case Names.Positional:
                return string.Join(DefaultArgumentSeparator, Values ?? []);
        }

        // Flag: --verbose -> just key
        if (Values is null or { Length: 0 })
        {
            return
                // core: Make sure there is no list suffix, which could indicate missing values.
                SuffixPattern.Match(Name) is { Length: > 0 } suffixMatch && suffixMatch.Groups["ItemList"].Success
                    ? throw new InvalidOperationException($"Argument '{Name}' has a list suffix but no values.")
                    : Name;
        }
        else
        {
            var name = Name;
            var nameValueSeparator = DefaultNameValueSeparator;

            // meta: Needs to check the length too as both patterns are optional so Success might be empty.
            if (SuffixPattern.Match(Name) is { Success: true, Length: > 0 } suffixMatch)
            {
                // meta: There is only a suffix and no argument name.
                if (suffixMatch.Index == 0)
                {
                    throw new InvalidOperationException($"Argument name cannot be empty or consist only of suffix: '{Name}'");
                }

                name = Name[..suffixMatch.Index];
                nameValueSeparator = suffixMatch.GroupValueOrDefault("NameValueSeparator", v => v, DefaultNameValueSeparator);
                var isList = suffixMatch.Groups["ItemList"].Success;
                var itemSeparator = suffixMatch.GroupValueOrDefault("ItemSeparator", v => v, DefaultItemSeparator);

                // List: --tags[] or --tags[,] => --tags a b or --tags a,b
                if (isList)
                {
                    var items = string.Join(itemSeparator, Values);
                    return $"{name}{nameValueSeparator}{items}";
                }
            }

            // Repeated: --file a --file b or --file=a --file=b
            return string.Join(DefaultArgumentSeparator, Values.Select(value => $"{name}{nameValueSeparator}{value}"));
        }
    }
}

public static class StepArgumentExtensions
{
    public static string Join(this IEnumerable<StepArgument> arguments)
    {
        return string.Join(" ", arguments.Select(cla => cla.ToString()));
    }

    public static StepArgument RenderValues(this StepArgument argument, IImmutableList<IVariableGroup> variables)
    {
        return argument with
        {
            Values = argument.Values?.Select(v => v.Render(variables)).ToArray()
        };
    }
}

public class StepArgumentConverter : JsonConverter<IImmutableList<StepArgument>>
{
    public override IImmutableList<StepArgument> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => ImmutableList<StepArgument>.Empty.Add(new StepArgument.Positional(reader.GetString()!)),
            JsonTokenType.StartObject => ParseArguments(ref reader, typeToConvert, options),
            _ => throw new JsonException($"Expected string or object but found {reader.TokenType}.")
        };
    }

    public override void Write(Utf8JsonWriter writer, IImmutableList<StepArgument> value, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }

    private static IImmutableList<StepArgument> ParseArguments(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDocument = JsonDocument.ParseValue(ref reader);

        return
            jsonDocument
                .RootElement
                .ReadStepArguments()
                .ToImmutableList();
    }
}

public static class JsonElementExtensions
{
    public static IEnumerable<StepArgument> ReadStepArguments(this JsonElement element)
    {
        return
            from property in element.EnumerateObject()
            let values = property.Value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.Array => property.Value.EnumerateArray().SelectPrimitives().ToArray(),
                JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => new[] { property.Value }.SelectPrimitives().ToArray(),
                _ => throw new JsonException($"Expected primitive or array for key '{property.Name}' but found '{property.Value.ValueKind}'.")
            }
            select new StepArgument(property.Name, values);
    }

    private static IEnumerable<string> SelectPrimitives(this IEnumerable<JsonElement> elements)
    {
        foreach (var element in elements)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False:
                    yield return element.ToString();
                    break;
                case JsonValueKind.Null or JsonValueKind.Undefined:
                    // core: Ignore these values.
                    break;
                default:
                    // core: Anything else is invalid.
                    throw new JsonException($"Array elements must be primitives, but found '{element.ValueKind}'.");
            }
        }
    }
}