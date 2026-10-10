using System;
using System.Threading.Tasks;
using Aion.Util.Data;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Buzz;

public class GetWorkflow
(
    ILogger<GetWorkflow> logger,
    DeserializeWorkflowSpec deserializeWorkflowSpec,
    GetProfile getProfile,
    CreateWorkflow createWorkflow
)
{
    public async Task<Workflow> For(WorkflowPath workflowPath, WorkflowPatch patch)
    {
        try
        {
            var spec = await deserializeWorkflowSpec.FromFile(workflowPath);
            var profile = await getProfile.For(workflowPath);
            return await createWorkflow.From(spec, profile, patch);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unable to load workflow '{WorkflowPath}'.", workflowPath);
            return Workflow.Disabled with { Path = workflowPath };
        }
    }
}