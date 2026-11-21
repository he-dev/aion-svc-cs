using System;
using Aion.Data;
using Quartz;

namespace Aion.Util;

public static class CreateWorkflowTrigger
{
    public static ITrigger Create
    (
        WorkflowPath workflowPath,
        WorkflowMode workflowMode,
        Action<TriggerBuilder<IJob>> configure
    )
    {
        // core: Cron and User triggers are different triggers.
        var jobName = $"{workflowPath.Name}@{workflowMode}";
        var jobGroup = "workflows";

        var builder =
            TriggerBuilder
                .Create()
                .ForJob(jobName, jobGroup)
                .WithIdentity(jobName, jobGroup)
                .UsingJobData(new JobDataMap
                {
                    { nameof(WorkflowPath), workflowPath },
                    { nameof(WorkflowMode), workflowMode }
                });
        configure(builder);
        return builder.Build();
    }
}