using System.IO;

namespace Aion.Util.Data;

public record WorkflowPath(string Home, string Workflow)
{
    public string Group => Path.GetDirectoryName(Workflow)!;

    public string Name => Path.GetFileName(Workflow);

    public static implicit operator string(WorkflowPath workflowPath)
    {
        return Path.Join(workflowPath.Home, workflowPath.Group, workflowPath.Workflow);
    }
}