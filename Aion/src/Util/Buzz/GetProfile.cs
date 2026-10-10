using System.IO;
using System.Threading.Tasks;
using Aion.Util.Data;

namespace Aion.Util.Buzz;

public class GetProfile(DeserializeWorkflowProfile deserializeWorkflowProfile)
{
    public async Task<WorkflowProfile> For(string workflowPath)
    {
        var profile = WorkflowProfile.Empty;
        var file = new FileInfo(workflowPath);
        for (var dir = file.Directory; dir != null; dir = dir.Parent)
        {
            profile += await deserializeWorkflowProfile.FromFile(Path.Join(dir.FullName, ".profile.json"));
            // note: This could stop at the scheduler-home.
        }

        return profile;
    }
}