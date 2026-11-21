using System.Linq;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Data.Configs;
using Aion.Util;
using Aion.Util.Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Aion.Tests.Util;

public class ValidateWorkflowConfigDefaultTest(TestWebApplication testApp) : IClassFixture<TestWebApplication>
{
    [Fact]
    public void EmptyProfileIsValid()
    {
        using var scope = testApp.Services.CreateScope();
        var validateWorkflowProfile = scope.ServiceProvider.GetRequiredService<ValidateWorkflowConfigDefault>();
        var profile = new WorkflowConfigDefault();
        Assert.True(validateWorkflowProfile.That(profile));
    }

    [Fact]
    public void ExistingLoggerIsValid()
    {
        using var scope = testApp.Services.CreateScope();
        var validateWorkflowProfile = scope.ServiceProvider.GetRequiredService<ValidateWorkflowConfigDefault>();

        var profile = new WorkflowConfigDefault { Logger = "file-28-days" };
        Assert.True(validateWorkflowProfile.That(profile));
    }

    [Fact]
    public void NonExistingLoggerIsNotValid()
    {
        using var scope = testApp.Services.CreateScope();
        var forkLogging = scope.ServiceProvider.GetRequiredService<ForkLogging>();
        var validateWorkflowProfile = scope.ServiceProvider.GetRequiredService<ValidateWorkflowConfigDefault>();

        var inMemoryFork = new InMemoryFork();
        using var unsubscribe = forkLogging.Into(inMemoryFork);

        // test: This logger does not exist.
        var profile = new WorkflowConfigDefault { Logger = "fake-28-days" };
        Assert.False(validateWorkflowProfile.That(profile));

        var errors = inMemoryFork.AtLeast(LogLevel.Error).ToList();
        Assert.Equal(1, errors.Count);

        var profileIssue = errors[0].Properties.ScalarOrDefault<string>("workflow.profile.issue");
        Assert.Equal($"Logger 'fake-28-days' does not exist.", profileIssue);
    } }