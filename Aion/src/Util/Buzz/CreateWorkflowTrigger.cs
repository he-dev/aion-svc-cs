using System;
using System.Linq;
using Aion.Util.Data;
using Quartz;

namespace Aion.Util.Buzz;

public static class CreateWorkflowTrigger
{
    private static ITrigger Create
    (
        WorkflowPath workflowPath,
        WorkflowTriggerMode workflowTriggerMode,
        Action<TriggerBuilder<IJob>> customize
    )
    {
        // core: Cron and User triggers are different triggers.
        var jobName = $"{workflowPath.Name}:{workflowTriggerMode}";
        var jobGroup = string.Join('/', new[] { "workflows", workflowPath.Directory }.Where(x => !string.IsNullOrEmpty(x)));

        var builder =
            TriggerBuilder
                .Create()
                .ForJob(jobName, jobGroup)
                .WithIdentity(jobName, jobGroup)
                .UsingJobData(new JobDataMap
                {
                    { nameof(WorkflowPath), workflowPath },
                    { nameof(WorkflowTriggerMode), workflowTriggerMode }
                });
        customize(builder);
        return builder.Build();
    }

    public static ITrigger Cron
    (
        WorkflowPath workflowPath,
        string cronExpression
    ) => Create
    (
        workflowPath,
        WorkflowTriggerMode.Cron,
        builder => builder.WithCronSchedule(cronExpression)
    );

    public static ITrigger Simple
    (
        WorkflowPath workflowPath,
        DateTimeOffset startAtUtc
    ) => Create
    (
        workflowPath,
        WorkflowTriggerMode.User,
        builder => builder.StartAt(startAtUtc).WithSimpleSchedule(x => x.WithRepeatCount(0))
    );

    public static ITrigger Never(WorkflowPath workflowPath) => Simple(workflowPath, DateTimeOffset.MinValue);
}