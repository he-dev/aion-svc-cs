using System.Threading.Tasks;
using Aion.Core.Hello.Jobs;
using Aion.Core.Magic.Workflows;
using Aion.Data;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Xunit;

namespace Aion.Tests.Util;

public class ScheduleWorkflowTest(TestWebApplication testApp) : IClassFixture<TestWebApplication>, IAsyncLifetime
{
    private IScheduler Scheduler { get; set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // meta: Gets a clean scheduler for each test.
        var factory = testApp.Services.GetRequiredService<ISchedulerFactory>();
        Scheduler = await factory.GetScheduler();
        await Scheduler.Clear();
    }

    [Fact]
    public async Task CanScheduleWorkflow()
    {
        using var scope = testApp.Services.CreateScope();
        var scheduleWorkflow = scope.ServiceProvider.GetRequiredService<ScheduleWorkflow>();
        await scheduleWorkflow.AsCron<WorkflowRegularJob>("**/hello-npc.json");

        var triggerKey = ScheduleWorkflow.TriggerKey(WorkflowMode.Job, new WorkflowPath("", "hello/hello-npc"));
        var trigger = Assert.IsAssignableFrom<ICronTrigger>(await Scheduler.GetTrigger(triggerKey));
        Assert.Equal("0/5 * * * * ?", trigger.CronExpressionString);

        var job = Assert.NotNull(await Scheduler.GetJobDetail(new JobKey(name: "hello/hello-npc", group: "job://workflows")));
    }

    [Fact]
    public async Task CanRescheduleWorkflow()
    {
        using var scope = testApp.Services.CreateScope();
        var scheduleWorkflow = scope.ServiceProvider.GetRequiredService<ScheduleWorkflow>();

        await scheduleWorkflow.AsCron<WorkflowRegularJob>("**/hello-npc.json");

        var triggerKey = ScheduleWorkflow.TriggerKey(WorkflowMode.Job, new WorkflowPath("", "hello/hello-npc"));

        var trigger = Assert.IsAssignableFrom<ICronTrigger>(await Scheduler.GetTrigger(triggerKey));
        Assert.Equal("0/5 * * * * ?", trigger.CronExpressionString);

        await scheduleWorkflow.AsCron<WorkflowRegularJob>("**/hello-npc.yaml");
        trigger = Assert.IsAssignableFrom<ICronTrigger>(await Scheduler.GetTrigger(triggerKey));
        Assert.Equal("0/5 1 * * * ?", trigger.CronExpressionString);

    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}