using System.IO;

namespace Aion.Util.Data;

public record WorkflowPath(string Home, string Workflow)
{
    public string Directory => Path.GetDirectoryName(Workflow)!;

    public override string ToString() => Path.Join(Home, Workflow);

    public static implicit operator string(WorkflowPath workflowPath) => workflowPath.ToString();
}