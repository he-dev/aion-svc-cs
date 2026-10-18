using System.Collections.Generic;

namespace Aion.Util.Data;

public class WorkflowProfileJson
{
    public Dictionary<string, string?>? Variables { get; init; }

    public string? Logger { get; init; }
}