using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Aion.Util.Buzz;
using Scriban.Runtime;

namespace Aion.Util.Data.Templating;

// core: Combines multiple variable groups into a single object.
public class CompositeVariableGroup : ScriptObject
{
    private static string Suffix { get; } = Regex.Replace(nameof(IVariableGroup), "^I", "", RegexOptions.IgnoreCase);

    private CompositeVariableGroup(IEnumerable<IVariableGroup> groups) : base(StringComparer.OrdinalIgnoreCase)
    {
        // core: Last group wins if there are multiple groups with the same name.
        foreach (var group in groups)
        {
            var groupKey = CreateGroupKey(group);
            if (this.TryGetValue(groupKey, out var value) && value is ScriptObject currentGroup)
            {
                // core: Import the group into the existing object overwriting the existing values with the new ones.
                currentGroup.Import(group);
            }
            else
            {
                Add(groupKey, group);
            }
        }
    }

    public static IScriptObject From(IEnumerable<IVariableGroup> groups) => new CompositeVariableGroup(groups);

    // core: Derives the group name from the class name by stripping the "VariableGroup" suffix.
    private static string CreateGroupKey(IVariableGroup group) => Regex.Replace(group.GetType().Name, Suffix + "$", "", RegexOptions.IgnoreCase);
}