using System.Linq;
using Aion.Util;
using Aion.Util.Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Aion.Tests.Util;

public class FindWorkflowsTest(TestWebApplication testApp) : IClassFixture<TestWebApplication>
{
    [Fact]
    public void FindsWorkflowByName()
    {
        using var scope = testApp.Services.CreateScope();
        var findWorkflows = scope.ServiceProvider.GetRequiredService<FindWorkflows>();
        var results =  findWorkflows.Where("**/hello-cmd");
        Assert.Equal(1, results.Count);
        Assert.Equal("hello/hello-cmd", results.Single().Name);
    }

    [Fact]
    public void IgnoresDuplicatesButLogsThem()
    {
        using var scope = testApp.Services.CreateScope();
        var forkLogging = scope.ServiceProvider.GetRequiredService<ForkLogging>();
        var findWorkflows = scope.ServiceProvider.GetRequiredService<FindWorkflows>();

        var inMemoryFork = new InMemoryFork();
        using var unsubscribe = forkLogging.Into(inMemoryFork);

        var results =  findWorkflows.Where("trash/duplicate");
        Assert.Equal(0, results.Count);

        var errors = inMemoryFork.AtLeast(LogLevel.Error).ToList();
        Assert.Equal(1, errors.Count);

        var workflowName = errors[0].Properties.ScalarOrDefault<string>("workflow.name");
        var workflowCount = errors[0].Properties.ScalarOrDefault<int>("workflow.count");

        Assert.Equal("trash/duplicate",  workflowName);
        Assert.Equal(2, workflowCount);
    }
}