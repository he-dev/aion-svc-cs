using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
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

        await scheduleWorkflow.AsCron("some-pattern");

        //var trigger = Assert.IsAssignableFrom<ICronTrigger>(await Scheduler.GetTrigger("trigger-key"));

        //Assert.Equal("expected-cron", trigger.CronExpressionString);
        //Assert.Equal("expected-job-key", trigger.JobKey);

        var job = Assert.NotNull(await Scheduler.GetJobDetail(new JobKey("job-key")));


    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}