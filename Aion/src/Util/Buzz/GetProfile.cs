using System.IO;
using System.Threading.Tasks;
using Aion.Meta;
using Aion.Util.Data;

namespace Aion.Util.Buzz;

public class GetProfile(DeserializeWorkflowProfile deserializeWorkflowProfile)
{
    public async Task<WorkflowProfile> For(WorkflowPath workflowPath)
    {
        var profile = WorkflowProfile.Empty;
        var file = new FileInfo(workflowPath);
        for (var dir = file.Directory; dir != null; dir = dir.Parent)
        {
            // core: Stop searching when we reach the scheduler-home.
            if (ComparePaths.AreEqual(dir.FullName, workflowPath.Home))
            {
                break;
            }

            profile += await deserializeWorkflowProfile.FromFile(Path.Join(dir.FullName, ".profile.json"));
        }

        return profile;
    }
}