using System;
using System.Diagnostics.CodeAnalysis;
using Aion.Util.Dotnet;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace Aion.Data.Templating;

// core: Get the value of a property from the source object.
public abstract class PropertyScriptObject() : ScriptObject(StringComparer.OrdinalIgnoreCase)
{
    public override bool TryGetValue(TemplateContext? context, SourceSpan span, string member, [MaybeNullWhen(true)] out object value)
    {
        if (this.TryGetProperty(member, out var property))
        {
            value = property.GetValue(this);
            return true;
        }

        if (base.TryGetValue(context, span, member, out value))
        {
            return true;
        }

        value = null!;
        return false;
    }

    public override bool CanWrite(string member) => false;
}