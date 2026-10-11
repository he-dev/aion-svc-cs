using System;
using System.Collections.Immutable;
using Aion.Util.Data.Templating;
using Scriban;
using Scriban.Functions;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Aion.Util.Buzz;

// https://github.com/scriban/scriban/tree/master/doc

public interface IVariableGroup : IScriptObject;

// util: Makes creating variable groups more convenient.
public static class RenderTemplate
{
    private const int MaxPassCount = 10;

    public static string Render(this string template, IImmutableList<IVariableGroup> variableGroups)
    {
        // note: Scriban's documentation recommends creating everything from scratch each time.
        var context = new TemplateContext
        {
            // core: Make sure no missing variable goes unnoticed.
            StrictVariables = true,
            // ReSharper disable once ConvertToLambdaExpression
            // hack: Scriban does not always throw exceptions when variables are missing despite the above flag. This way it does.
            TryGetMember = (context, span, target, member, out value) =>
            {
                // hack: This is a fake comment, so that ReSharper does not put everything in a single line.
                throw new ScriptRuntimeException(span, $"Variable '{member}' not found while rendering '{context.CurrentGlobal}'.");
            }
        };

        context.PushGlobal(ScriptObject.From(typeof(BuiltinFunctions)));
        context.PushGlobal(ScriptObject.From(typeof(StringFunctions)));
        context.PushGlobal(CompositeVariableGroup.From(variableGroups.Add(new EnvironmentVariableGroup())));

        var current = template;
        var previous = string.Empty;
        var passCount = 0;

        var parserOptions = new ParserOptions
        {
            ExpressionDepthLimit = 10
        };

        // core: Ensure we also render nested variables but don't fall into an infinite loop.
        while (current != previous && passCount < MaxPassCount)
        {
            previous = current;
            try
            {
                current = Template.Parse(current, parserOptions: parserOptions).Render(context);
            }
            catch (Exception ex)
            {
                throw new Exception($"Template '{template}' could not be rendered.", ex);
            }

            // core: Apparently it's still not fully rendered.
            if (++passCount > MaxPassCount)
            {
                throw new Exception($"Template '{template}' did not stabilize after {MaxPassCount} passes at '{current}'.");
            }
        }

        return current;
    }
}