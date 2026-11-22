using Quartz;

namespace Aion.Data;

public static class WorkflowTrigger
{
    public static TriggerBuilder<IJob> UsingJobData
    (
        WorkflowPath workflowPath,
        WorkflowMode workflowMode
    )
    {
        // mode://workflows.hello/hello-npc
        var jobName = $"{workflowPath.Name}".ToLowerInvariant();
        var jobGroup = $"{workflowMode}://workflows".ToLowerInvariant();

        return
            TriggerBuilder
                .Create()
                .ForJob(jobName, jobGroup)
                .WithIdentity(jobName, jobGroup)
                .UsingJobData(nameof(WorkflowPath), workflowPath)
                .UsingJobData(nameof(WorkflowMode), workflowMode);
    }
}