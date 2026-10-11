using System;
using Aion.Util.Buzz;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Aion.Util.Data.Templating;

public class EnvironmentVariableGroup : ScriptObject, IVariableGroup
{
    // meta: Scriban calls this for BOTH Environment.Name and Environment["Name"]
    public override bool TryGetValue(TemplateContext context, SourceSpan span, string member, out object value)
    {
        // core: Fetch from OS
        var temp = Environment.GetEnvironmentVariable(member);

        if (temp is null)
        {
            throw new ScriptRuntimeException(span, $"Environment variable '{member}' not defined.");
        }

        if (string.IsNullOrEmpty(temp))
        {
            throw new ScriptRuntimeException(span, $"Environment variable '{member}' is empty.");
        }

        value = temp;
        return true;
    }
}