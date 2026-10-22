using System.IO;

namespace Aion.Util.Data;

public record WorkflowPath(string Home, string Name)
{
    public string Directory => Path.GetDirectoryName(Name)!;

    public override string ToString() => Path.Join(Home, Name);

    public static implicit operator string(WorkflowPath workflowPath) => workflowPath.ToString();
}