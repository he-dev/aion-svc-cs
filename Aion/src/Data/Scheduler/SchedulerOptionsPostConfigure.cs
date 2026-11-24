using System.IO;
using Aion.Util;
using Aion.Util.Dotnet;
using Microsoft.Extensions.Options;

namespace Aion.Data.Scheduler;

public class SchedulerOptionsPostConfigure : IPostConfigureOptions<SchedulerOptions>
{
    public void PostConfigure(string? optionsName, SchedulerOptions scheduler)
    {
        // core: Renders the home path.
        scheduler.Home = RenderTemplate.String(scheduler.Home, []);
        scheduler.Home = Path.GetFullPath(scheduler.Home).ToSlash();
    }
}