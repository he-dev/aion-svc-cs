using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Objective.Workflows;
using Aion.Core.Services;
using Aion.Util;
using Aion.Util.Buzz.Synchronizations;
using Aion.Util.Data;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Xunit;

namespace Aion.Tests.Core.Services.Commands;

public class TestScheduleWorkflow(TestWebApplication testWebApplication) : IClassFixture<TestWebApplication>
{
    // core: Synchronizes each specified workflow and returns their results.
    private async IAsyncEnumerable<ScheduleChange> SynchronizeWorkflow(params WorkflowSpec[] workflows)
    {
        using var scope = testWebApplication.Services.CreateScope();
        var scheduleWorkflow = scope.ServiceProvider.GetRequiredService<ScheduleWorkflow>();

        try
        {
            foreach (var workflow in workflows)
            {
                yield return await scheduleWorkflow.Invoke(workflow);
            }
        }
        finally
        {
            // meta: Clear all schedules for the next test.
            var scheduler = await scope.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            await scheduler.Clear();
        }
    }

    private static readonly WorkflowSpec EmptyWorkflowDraft = new WorkflowSpec
    {
        Disabled = true,
        Cron = "0/5 * * * * ?",
        Path = new WorkflowPath(@"C:\fake\path\to\profiles\tests", new WorkflowName("fake-workflow")),
        Steps = []
    };

    [Fact]
    public async Task IgnoresDisabledWorkflow()
    {
        var results = await SynchronizeWorkflow(EmptyWorkflowDraft with { Disabled = false }).ToListAsync();
        Assert.Single(results);
        Assert.Null(results.First().Action);
    }

    [Fact]
    public async Task IgnoresEmptyWorkflow()
    {
        var results = await SynchronizeWorkflow(EmptyWorkflowDraft with { Disabled = true }).ToListAsync();
        Assert.Single(results);
        Assert.Null(results.First().Action);
    }

    [Fact]
    public async Task IgnoresUnchangedWorkflow()
    {
        var results = await SynchronizeWorkflow
        (
            EmptyWorkflowDraft with { Disabled = true, Steps = [new WorkflowStepSpec { Disabled = true, FileName = @"c:\fake\path\to\fake.exe" }] },
            EmptyWorkflowDraft with { Disabled = true, Steps = [new WorkflowStepSpec { Disabled = true, FileName = @"c:\fake\path\to\fake.exe" }] }
        ).ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(nameof(ScheduleRegularWorkflow), results.First().Action);
        Assert.Null(results.Last().Action);
    }

    [Fact]
    public async Task UnschedulesDisabledWorkflow()
    {
        var results = await SynchronizeWorkflow
        (
            EmptyWorkflowDraft with { Disabled = true, Steps = [new WorkflowStepSpec { Disabled = true, FileName = @"c:\fake\path\to\fake.exe" }] },
            EmptyWorkflowDraft with { Disabled = false, Steps = [new WorkflowStepSpec { Disabled = true, FileName = @"c:\fake\path\to\fake.exe" }] }
        ).ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(nameof(ScheduleRegularWorkflow), results[0].Action);
        Assert.Equal(nameof(UnscheduleDisabledWorkflow), results[1].Action);
    }

    [Fact]
    public async Task UnschedulesEmptyWorkflow()
    {
        var results = await SynchronizeWorkflow
        (
            EmptyWorkflowDraft with { Disabled = true, Steps = [new WorkflowStepSpec { Disabled = true, FileName = @"c:\fake\path\to\fake.exe" }] },
            EmptyWorkflowDraft with { Disabled = true, Steps = [] }
        ).ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(nameof(ScheduleRegularWorkflow), results.First().Action);
        Assert.Equal(nameof(UnscheduleEmptyWorkflow), results.Last().Action);
    }

    [Fact]
    public async Task ReschedulesChangedWorkflow()
    {
        var results = await SynchronizeWorkflow
        (
            EmptyWorkflowDraft with { Disabled = true, Cron = "0/5 * * * * ?", Steps = [new WorkflowStepSpec { Disabled = true, FileName = @"c:\fake\path\to\fake.exe" }] },
            EmptyWorkflowDraft with { Disabled = true, Cron = "0/6 * * * * ?", Steps = [new WorkflowStepSpec { Disabled = true, FileName = @"c:\fake\path\to\fake.exe" }] }
        ).ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(nameof(ScheduleRegularWorkflow), results.First().Action);
        Assert.Equal(nameof(RescheduleChangedWorkflow), results.Last().Action);
    }

    [Fact]
    public async Task SchedulesNewWorkflow()
    {
        var results = await SynchronizeWorkflow
        (
            EmptyWorkflowDraft with { Disabled = true, Steps = [new WorkflowStepSpec { Disabled = true, FileName = @"c:\fake\path\to\fake.exe" }] }
        ).ToListAsync();

        Assert.Single(results);
        Assert.Equal(nameof(ScheduleRegularWorkflow), results.First().Action);
    }
}