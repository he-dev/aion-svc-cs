using System;
using System.Diagnostics.CodeAnalysis;
using Aion.Meta;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace Aion.Util.Data.Templating;

// core: Get the value of a property from the source object.
public abstract class PropertyScriptObject() : ScriptObject(StringComparer.OrdinalIgnoreCase)
{
    public override bool TryGetValue(TemplateContext? context, SourceSpan span, string member, [MaybeNullWhen(true)] out object value)
    {
        if (!this.TryGetProperty(member, out var property))
        {
            value = null!;
            return false;
        }

        value = property.GetValue(this);
        return true;
    }

    public override bool CanWrite(string member) => false;
}