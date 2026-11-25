using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
using Aion.Data;
using Aion.Util;
using Aion.Util.Serilog;
using Microsoft.Extensions.DependencyInjection;
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

        var exitCodes = inMemoryFork.WhereProperty<int>("workflow.step.exit_code", x => x == 0).ToList();
        Assert.Equal(1, exitCodes.Count);
    }
}