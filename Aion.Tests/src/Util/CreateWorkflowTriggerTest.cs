using Aion.Data;
using Aion.Util;
using Quartz;
using Xunit;

namespace Aion.Tests.Util;

public class CreateWorkflowTriggerTest
{
    [Fact]
    public void CanCreateCronTrigger()
    {
        var trigger = CreateWorkflowTrigger.Create
        (
            new WorkflowPath("C:/path/to/workflows", "some/workflow.json"),
            WorkflowTriggerSource.Job,
            builder => builder.WithCronSchedule("0/5 * * * * ?")
        );
        var cron = Assert.IsAssignableFrom<ICronTrigger>(trigger);
        Assert.Equal("workflows.some/workflow@Job", cron.Key.ToString());
    }
}