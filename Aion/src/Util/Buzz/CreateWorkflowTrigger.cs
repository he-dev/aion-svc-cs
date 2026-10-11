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
        WorkflowMode workflowMode,
        Action<TriggerBuilder<IJob>> customize
    )
    {
        var group = string.Join('/', new[] { "workflows", workflowPath.Directory }.Where(x => !string.IsNullOrEmpty(x)));
        var jobKey = new JobKey(workflowPath.Workflow, group);

        var builder =
            TriggerBuilder
                .Create()
                .ForJob(jobKey)
                // core: Cron and User triggers are different triggers.
                .WithIdentity($"{workflowPath.Workflow}:{workflowMode}", group)
                .UsingJobData(new JobDataMap
                {
                    { nameof(WorkflowPath), workflowPath },
                    { nameof(WorkflowMode), workflowMode }
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