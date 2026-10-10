using System.Collections.Immutable;
using Aion.Util.Data.Templating.Services;
using Microsoft.Extensions.Options;

namespace Aion.Util.Data.Scheduler;

public class SchedulerOptionsPostConfigure : IPostConfigureOptions<SchedulerOptions>
{
    public void PostConfigure(string? optionsName, SchedulerOptions scheduler)
    {
        // core: Renders the home path.
        scheduler.Home = scheduler.Home.Render(ImmutableList<IVariableGroup>.Empty);
    }
}