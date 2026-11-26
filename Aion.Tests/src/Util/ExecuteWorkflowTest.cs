using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
using Aion.Data;
using Aion.Data.Configs;
using Aion.Util;
using Aion.Util.Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Aion.Tests.Util;

public class ExecuteWorkflowTest(TestWebApplication testApp) : IClassFixture<TestWebApplication>
{
    [Fact]
    public async Task CanExecuteWorkflow()
    {
        using var scope = testApp.Services.CreateScope();
        var forkLogging = scope.ServiceProvider.GetRequiredService<ForkLogging>();
        var findWorkflows = scope.ServiceProvider.GetRequiredService<FindWorkflows>();
        var loadWorkflow = scope.ServiceProvider.GetRequiredService<LoadWorkflow>();
        var executeWorkflow = scope.ServiceProvider.GetRequiredService<ExecuteWorkflow>();

        var inMemoryFork = new InMemoryFork();
        using var unsubscribeFork = forkLogging.Into(inMemoryFork);

        var workflowPaths = findWorkflows.Where("**/tests/one-step");
        var workflowSpec = await loadWorkflow.Spec(workflowPaths.Single());
        await executeWorkflow.Invoke(workflowSpec!, WorkflowMode.Job, []);

        var exitCodes = inMemoryFork.WhereScalar<int>("workflow.step.exit_code", x => x == 0).ToList();
        Assert.Equal(1, exitCodes.Count);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task CanExecuteTypicalWorkflowScenarios(string[] testCase)
    {
        using var scope = testApp.Services.CreateScope();
        var forkLogging = scope.ServiceProvider.GetRequiredService<ForkLogging>();
        var loadWorkflow = scope.ServiceProvider.GetRequiredService<LoadWorkflow>();
        var executeWorkflow = scope.ServiceProvider.GetRequiredService<ExecuteWorkflow>();

        var inMemoryFork = new InMemoryFork();
        using var unsubscribeFork = forkLogging.Into(inMemoryFork);

        // core: Create a test-config from the test-case.
        var config = new WorkflowConfig
        {
            Enabled = true,
            Cron = "0 0/5 * * * ?",
            Variables =
            {
                ["NpcExe"] = "{{ Environment.USERPROFILE }}/Stuff/craft/coding/aion-svc-cs/Aion.Npc/bin/Debug/net10.0/Aion.Npc.exe"
            },
            Steps = [.. testCase.Select(options => new StepConfig().Configure(options))]
        };

        // core: Create the expected results form the test-case.
        var expected = testCase.Select(ExpectedStepResult.Parse).ToList();

        var workflowSpec = Assert.NotNull(loadWorkflow.Spec("test", config));
        await executeWorkflow.Invoke(workflowSpec, WorkflowMode.Job, []);

        //var errors = inMemoryFork.AtLeast(LogLevel.Error);
        //Assert.Empty(errors);

        var exitCodes = inMemoryFork.WhereScalar<int>("workflow.step.exit_code", _ => true).SelectScalar("workflow.step.exit_code", (int?)null).ToList();

        Assert.Equal(3, exitCodes.Count);


        foreach (var (e, a) in expected.Zip(exitCodes, (e, a) => (e, a)))
        {
            Assert.Equal(e.ExitCode, a);
        }
    }

    public static IEnumerable<TheoryDataRow<string[]>> TestData()
    {
        yield return new(["okay, long, time", "skip", "fail", "okay"]);
    }
}

public static class Extensions
{
    public static StepConfig Configure(this StepConfig config, string options)
    {
        config = config with
        {
            Name = options,
            Enabled = true,
            FileName = "{{ Workflow.NpcExe }}"
        };

        return options.Split(',').Aggregate(config, (current, option) => option.Trim() switch
        {
            "skip" => current with { Enabled = false },
            "okay" => current with { Arguments = [.. current.Arguments, "--exit-code 0"] },
            "fail" => current with { Arguments = [.. current.Arguments, "--exit-code 1"] },
            "long" => current with { Arguments = [.. current.Arguments, "--sleep 3"] },
            "time" => current with { Timeout = TimeSpan.FromSeconds(1) },
            _ => current
        });
    }
}

public record ExpectedStepResult
{
    public int? ExitCode { get; init; }

    public TimeSpan Duration { get; init; }

    public static ExpectedStepResult Parse(string options)
    {
        return options.Split(',').Aggregate(new ExpectedStepResult(), (current, option) => option.Trim() switch
        {
            "skip" => current with { ExitCode = null },
            "okay" => current with { ExitCode = 0 },
            "fail" => current with { ExitCode = 1 },
            "long" => current with { Duration = Shorter(current.Duration, TimeSpan.FromSeconds(3)) },
            "time" => current with { Duration = Shorter(current.Duration, TimeSpan.FromSeconds(1)) },
            _ => current
        });
    }

    private static TimeSpan Shorter(TimeSpan current, TimeSpan candidate)
    {
        return current == TimeSpan.Zero || candidate < current ? candidate : current;
    }
}