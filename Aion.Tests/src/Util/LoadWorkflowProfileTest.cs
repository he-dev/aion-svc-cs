using System.Linq;
using System.Threading.Tasks;
using Aion.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aion.Tests.Util;

public class LoadWorkflowProfileTest(TestWebApplication testApp): IClassFixture<TestWebApplication>
{
    [Fact]
    public async Task MergesProfiles()
    {
        using var scope = testApp.Services.CreateScope();
        var findWorkflows = scope.ServiceProvider.GetRequiredService<FindWorkflows>();
        var loadWorkflowProfile =  scope.ServiceProvider.GetRequiredService<LoadWorkflowProfile>();

        var workflowPath = findWorkflows.Where("**/hello-cmd").Single();
        var workflowProfile = await loadWorkflowProfile.FromJsonOrEmpty(workflowPath);
        Assert.NotNull(workflowProfile);
        Assert.Equal("one", workflowProfile.Variables["one"]);
        Assert.Equal("two", workflowProfile.Variables["two"]);
        Assert.Equal("file-28-days", workflowProfile.Logger);
    }
}