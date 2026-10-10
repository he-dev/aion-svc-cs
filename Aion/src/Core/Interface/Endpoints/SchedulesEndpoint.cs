using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Interface.Endpoints.Filters;
using Aion.Core.Objective.Workflows;
using Aion.Meta;
using Aion.Meta.Quartz;
using Aion.Util.Data.Scheduler;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace Aion.Core.Interface.Endpoints;

public static class SchedulesEndpoint
{
    public static void MapSchedules(this WebApplication app)
    {
        var workflows = app.MapGroup("api/profiles/{profileName}/schedules").AddEndpointFilter<EnsureProfileExists>();
        workflows.MapGet("", GetSchedules);
    }

    private static async Task<IResult> GetSchedules(GetProfileTriggers getProfileTriggers, string profileName, [FromQuery(Name = "q")] string? workflowFilter)
    {
        var triggers = await getProfileTriggers.Where(profileName).ToListAsync();

        // meta: Keep it stable.
        var utcNow = DateTimeOffset.UtcNow;
        return Results.Ok(new
        {
            profile = profileName,
            pattern = workflowFilter,
            schedules =
                from trigger in triggers
                where trigger.JobKey.Name.IsLike(workflowFilter)
                let next = ((ICronTrigger)trigger).FiresAt(utcNow).Take(3)
                orderby next.FirstOrDefault()
                select new
                {
                    workflow = trigger.JobDataMap.GetString(JobDataKeys.WorkflowPath),
                    cron = ((ICronTrigger)trigger).CronExpressionString,
                    next
                }
        });
    }
}