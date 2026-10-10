using System;
using System.Threading.Tasks;
using Aion.Util.Data.Scheduler;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aion.Core.Interface.Endpoints.Filters;

public class EnsureProfileExists : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // meta: Get the profileName parameter from the route.
        if (!context.HttpContext.Request.RouteValues.TryGetValue("profileName", out var profileNameObj) || profileNameObj is not string profileName)
        {
            return Results.BadRequest("Profile name is required.");
        }

        // meta: Get the engine options from DI.
        var schedulerOptions = context.HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<SchedulerOptions>>();

        // core: Check the profile exists.
        //if (!schedulerOptions.Value.Profiles.Any(p => p.Value.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.NotFound($"Profile '{profileName}' not found.");
        }

        return await next(context);
    }
}