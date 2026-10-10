using System;
using System.Linq;
using JetBrains.Annotations;
using Serilog;

namespace Aion.Meta.Serilog;

public static class RenderMessageTemplate
{
    public static string From([StructuredMessageTemplate] string template, params object?[] args)
    {
        if (!Log.BindMessageTemplate(template, args, out var parsed, out var properties))
        {
            throw new ArgumentException($"Cannot bind message template: '{template}'");
        }

        return parsed.Render(properties.ToDictionary(p => p.Name, p => p.Value), null);
    }
}