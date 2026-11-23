using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
using Aion.Data;
using Aion.Util;
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
    public async Task SchedulesWorkflow()
    {
        using var scope = testApp.Services.CreateScope();
        var scheduleWorkflow = scope.ServiceProvider.GetRequiredService<ScheduleWorkflow>();
        await scheduleWorkflow.AsCron("**/hello-npc.json");

        var trigger = Assert.IsAssignableFrom<ICronTrigger>(await Scheduler.GetTrigger(new TriggerKey(name: "hello/hello-npc", group: "job://workflows")));
        Assert.Equal("0/5 * * * * ?", trigger.CronExpressionString);

        var job = Assert.NotNull(await Scheduler.GetJobDetail(new JobKey(name: "hello/hello-npc", group: "job://workflows")));
        Assert.True(trigger.JobDataMap.ContainsKey(nameof(WorkflowPath)));
        Assert.True(trigger.JobDataMap.ContainsKey(nameof(WorkflowMode)));
    }

    [Fact]
    public async Task ReSchedulesWorkflow()
    {
        using var scope = testApp.Services.CreateScope();
        var scheduleWorkflow = scope.ServiceProvider.GetRequiredService<ScheduleWorkflow>();

        await scheduleWorkflow.AsCron("**/hello-npc.json");
        var trigger = Assert.IsAssignableFrom<ICronTrigger>(await Scheduler.GetTrigger(new TriggerKey(name: "hello/hello-npc", group: "job://workflows")));
        Assert.Equal("0/5 * * * * ?", trigger.CronExpressionString);
        Assert.NotNull(await Scheduler.GetJobDetail(new JobKey(name: "hello/hello-npc", group: "job://workflows")));

        await scheduleWorkflow.AsCron("**/hello-npc.yaml");
        trigger = Assert.IsAssignableFrom<ICronTrigger>(await Scheduler.GetTrigger(new TriggerKey(name: "hello/hello-npc", group: "job://workflows")));
        Assert.Equal("0/5 1 * * * ?", trigger.CronExpressionString);
        Assert.NotNull(await Scheduler.GetJobDetail(new JobKey(name: "hello/hello-npc", group: "job://workflows")));

    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}