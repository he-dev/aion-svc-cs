using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Aion.Data;

public record WorkflowPattern(string Value) : IEnumerable<string>
{
    public static readonly ImmutableList<string> Extensions = [".json", ".yaml", ".yml"];

    public static readonly WorkflowPattern Any = new("**/*");

    public static WorkflowPattern Parse(string? pattern)
    {
        return string.IsNullOrWhiteSpace(pattern) ? Any : new WorkflowPattern(pattern);
    }

    public IEnumerator<string> GetEnumerator()
    {
        // core: When the pattern contains the file extension, assume it points to a specific file.
        if (Extensions.Any(ext => Value.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            yield return Value;
        }
        else
        {
            foreach (var extension in Extensions)
            {
                yield return $"{Value}{extension}";
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static implicit operator WorkflowPattern(string pattern) => Parse(pattern);
}