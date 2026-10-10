using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Interface.Endpoints.Filters;
using Aion.Core.Objective.Downtimes;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aion.Core.Interface.Endpoints;

public static class DowntimesEndpoint
{
    public static void MapDowntimes(this WebApplication app)
    {
        var downtimes = app
            .MapGroup("api")
            .AddEndpointFilter<EnsureProfileExists>();
        downtimes.MapPost("downtimes", GetDowntimes);
        downtimes.MapPost("downtimes:start", Start);
        downtimes.MapPost("downtimes:end", EndNow);
    }

    private static async Task<IResult> GetDowntimes(GetDowntimes getDowntimes)
    {
        var downtimes = await getDowntimes.Where(null);
        return Results.Ok(new
        {
            downtimes =
                from downtime in downtimes
                orderby downtime.Remaining descending, downtime.Duration descending
                select new
                {
                    downtime.Pattern,
                    StartsOn = downtime.StartsOnUtc.ToLocalTime(),
                    EndsOn = downtime.EndsOnUtc.ToLocalTime(),
                    downtime.Duration,
                    downtime.Remaining,
                    Status = downtime.Status.ToString()
                }
        });
    }

    private static async Task<IResult> Start(StartDowntime startDowntime, [FromBody] DowntimeStartBody body)
    {
        try
        {
            var downtime = WorkflowDowntime.At(body.Pattern, body.StartsOnUtc, body.EndsOnUtc);
            var results = await startDowntime.Now(body.Pattern, downtime);

            return Results.Accepted($"/api/downtimes", new
            {
                downtime = new
                {
                    StartsAt = downtime.StartsOnUtc.ToLocalTime(),
                    EndsAt = downtime.EndsOnUtc.ToLocalTime(),
                    downtime.Duration,
                    downtime.Remaining,
                    Status = downtime.Status.ToString()
                },
                locks =
                    from result in results
                    select new { Workflow = result.WorkflowPath, Error = result.Exception?.Message }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.ToString(), statusCode: 500);
        }
    }

    private static async Task<IResult> EndNow(EndDowntime endDowntime, [FromBody] DowntimeEndNowBody? body)
    {
        var downtimes = await endDowntime.Now(body?.Pattern);
        return Results.Ok(new
        {
            pattern = body?.Pattern,
            downtimes =
                from downtime in downtimes
                orderby downtime.Remaining descending, downtime.Duration descending
                select new
                {
                    downtime.Pattern,
                    StartsOn = downtime.StartsOnUtc.ToLocalTime(),
                    EndsOn = downtime.EndsOnUtc.ToLocalTime(),
                    downtime.Duration,
                }
        });
    }
}

public record DowntimeStartBody
{
    public string Pattern { get; init; } = null!;
    public TimeSpan? Wait { get; init; }
    public DateTime? StartsOn { get; init; }
    public TimeSpan? Duration { get; init; }
    public DateTime? EndsOn { get; init; }

    public DateTimeOffset StartsOnUtc
    {
        get
        {
            if (StartsOn is null && Wait is null) throw new InvalidOperationException("Downtime must have either a start time or a wait time.");
            return StartsOn?.ToUniversalTime() ?? DateTimeOffset.UtcNow + (Wait ?? TimeSpan.Zero);
        }
    }

    public DateTimeOffset EndsOnUtc
    {
        get
        {
            if (EndsOn is null && Duration is null) throw new InvalidOperationException("Downtime must have an end time.");
            return EndsOn?.ToUniversalTime() ?? StartsOnUtc + (Duration ?? TimeSpan.Zero);
        }
    }
}

public record DowntimeEndNowBody
{
    public string Pattern { get; init; } = null!;
}