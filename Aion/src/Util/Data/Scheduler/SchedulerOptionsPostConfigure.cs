using System.Collections.Immutable;
using System.IO;
using Aion.Meta;
using Aion.Util.Buzz;
using Microsoft.Extensions.Options;

namespace Aion.Util.Data.Scheduler;

public class SchedulerOptionsPostConfigure : IPostConfigureOptions<SchedulerOptions>
{
    public void PostConfigure(string? optionsName, SchedulerOptions scheduler)
    {
        // core: Renders the home path.
        scheduler.Home = scheduler.Home.Render(ImmutableList<IVariableGroup>.Empty);
        scheduler.Home = Path.GetFullPath(scheduler.Home).ToSlash();
    }
}