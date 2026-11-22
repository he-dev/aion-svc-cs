using Aion.Data;
using Aion.Util;
using Quartz;
using Xunit;

namespace Aion.Tests.Util;

public class WorkflowTriggerTest
{
    [Fact]
    public void CanCreateCronTrigger()
    {
        var trigger =
            WorkflowTrigger
                .UsingJobData(new WorkflowPath("C:/path/to/workflows", "some/workflow.json"), WorkflowMode.Job)
                .WithCronSchedule("0/5 * * * * ?")
                .Build();
        var cron = Assert.IsAssignableFrom<ICronTrigger>(trigger);
        Assert.Equal("job://workflows.some/workflow", cron.Key.ToString());
    }
}