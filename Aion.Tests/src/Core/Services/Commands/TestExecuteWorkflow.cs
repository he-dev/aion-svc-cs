using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aion.Core.Objective.Workflows;
using Aion.Core.Services;
using Aion.Util;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aion.Tests.Core.Services.Commands;

public class TestExecuteWorkflow(TestWebApplication testWebApplication) : IClassFixture<TestWebApplication>
{
    [Theory]
    [InlineData("c1.ok")]
    [InlineData("c1.disabled")]
    [InlineData("c1.error-3")]
    [InlineData("c1.timeout-3s")]
    // [InlineData("c2.failure-1_depends")]
    // [InlineData("c3.failure-1_depends_depends")]
    // [InlineData("c3.success_offline_success")]
    // [InlineData("c3.success_failure-1_success")]
    // [InlineData("c3.offline_offline_success")]
    public async Task CanExecuteTypicalWorkflowScenarios(string testCase)
    {
        using var scope = testWebApplication.Services.CreateScope();

        var createWorkflow = scope.ServiceProvider.GetRequiredService<ComposeWorkflow>();
        var executeWorkflow = scope.ServiceProvider.GetRequiredService<ExecuteWorkflow>();
        var getProfile = scope.ServiceProvider.GetRequiredService<GetProfiles>();
        var findWorkflows = scope.ServiceProvider.GetRequiredService<FindWorkflows>();

        var profile = getProfile.Single("tests");
        var workflowPath = findWorkflows.Single(new WorkflowSearchCriteria(profile, testCase));
        var workflowTemplate = await WorkflowSpec.FromFile(workflowPath);
        var workflow = await createWorkflow.From(workflowTemplate);
        var actual = await executeWorkflow.Invoke(workflow);
        var expected = WorkflowTestCase.Parse(testCase).ToList();

        Assert.Equal(expected.Count, actual.Count);
        foreach (var (e, a) in expected.Zip(actual, (e, a) => (e, a)))
        {
            Assert.Equal(e.ExitCode, a.ExitCode);
            Assert.Equal(e.Status, a.Status);

        }
    }
}

public static class WorkflowTestCase
{
    public static IEnumerable<ExpectedStepResult> Parse(string testName)
    {
        // "c3.1-null-0" -> count=3, exitCodes=[1, null, 0]
        var parts = testName[1..].Split('.');
        //var count = int.Parse(parts[0]);
        foreach (var testCase in parts[1].Split('_'))
        {
            yield return ExpectedStepResult.Parse(testCase);
        }
    }
}

public record ExpectedStepResult(int? ExitCode, StepStatus Status, TimeSpan Duration)
{
    private static readonly Regex StatusOk = new(@"ok(-(?<durationSeconds>\d+)s)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StatusError = new(@"error-(?<exitCode>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StatusTimeout = new(@"timeout-(?<timeoutSeconds>\d+)s", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StatusDisabled = new(@"disabled", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ExpectedStepResult Parse(string testCase)
    {
        if (StatusOk.Match(testCase) is { Success: true } ok)
        {
            var duration =
                ok.Groups["durationSeconds"].Success
                    ? TimeSpan.FromSeconds(int.Parse(ok.Groups["durationSeconds"].Value))
                    : TimeSpan.Zero;
            return new ExpectedStepResult(ExitCode: 0, StepStatus.Okay, duration);
        }

        if (StatusError.Match(testCase) is { Success: true } error)
        {
            var exitCode = int.Parse(error.Groups["exitCode"].Value);
            return new ExpectedStepResult(exitCode, StepStatus.Fail, TimeSpan.Zero);
        }

        if (StatusTimeout.Match(testCase) is { Success: true } timeout)
        {
            var timeoutSeconds = int.Parse(timeout.Groups["timeoutSeconds"].Value);
            return new ExpectedStepResult(null, StepStatus.Timeout, TimeSpan.FromSeconds(timeoutSeconds));
        }

        if (StatusDisabled.Match(testCase) is { Success: true })
        {
            return new ExpectedStepResult(null, StepStatus.None, TimeSpan.Zero);
        }

        throw new ArgumentOutOfRangeException(nameof(testCase), testCase, "Invalid test case.");
    }
}
