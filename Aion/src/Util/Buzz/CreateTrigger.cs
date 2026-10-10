using System;
using Aion.Meta.Quartz;
using Aion.Util.Data;
using Quartz;

namespace Aion.Util.Buzz;

public static class CreateTrigger
{
    private static ITrigger Create
    (
        WorkflowPath workflowPath,
        WorkflowMode workflowMode,
        Action<TriggerBuilder<IJob>> customize
    )
    {
        var jobKey = new JobKey(workflowPath.Workflow, workflowPath.Group);

        var builder =
            TriggerBuilder
                .Create()
                .ForJob(jobKey)
                .WithIdentity($"{workflowPath.Workflow}:{workflowMode}", workflowPath.Group)
                .UsingJobData(nameof(WorkflowPath), workflowPath.Workflow)
                .UsingJobData(workflowMode);
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
        WorkflowMode.Cron,
        builder => builder.WithCronSchedule(cronExpression));

    public static ITrigger Simple
    (
        WorkflowPath workflowPath,
        DateTimeOffset startAtUtc
    ) => Create
    (
        workflowPath,
        WorkflowMode.User,
        builder => builder.StartAt(startAtUtc).WithSimpleSchedule(x => x.WithRepeatCount(0))
    );
}