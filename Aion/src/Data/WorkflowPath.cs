using System.IO;
using Aion.Util.Dotnet;

namespace Aion.Data;

public record WorkflowPath(string Root, string Relative)
{
    public string Directory => Path.GetDirectoryName(Relative)!;

    public string Name => Path.Join(Path.GetDirectoryName(Relative), Path.GetFileNameWithoutExtension(Relative)).ToSlash();

    public string Extension => Path.GetExtension(Relative);

    public override string ToString() => Path.Join(Root, Relative).ToSlash();

    public static implicit operator string(WorkflowPath workflowPath) => workflowPath.ToString();
}