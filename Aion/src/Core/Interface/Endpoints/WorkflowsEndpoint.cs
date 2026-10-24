using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Objective.Workflows;
using Aion.Meta.Serilog;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Interface.Endpoints;

public static class WorkflowsEndpoint
{
    public static void MapWorkflows(this WebApplication app)
    {
        //var workflows = app.MapGroup("api/profiles/{profileName}").AddEndpointFilter<EnsureProfileExists>();
        var workflows = app.MapGroup("api");
        workflows.MapGet("workflows", GetWorkflows);
        workflows.MapPost("workflows:start-now", StartNow);
        workflows.MapPost("workflows:start-in", StartIn);
        workflows.MapPost("workflows:start-at", StartAt);
        workflows.MapPost("workflows:sync", Synchronize);
    }

    private static async Task<IResult> GetWorkflows(ComposeWorkflowOverview composeWorkflowOverview, [FromBody] WorkflowQuery? body)
    {
        var workflows = await composeWorkflowOverview.Where(body?.Pattern);
        return Results.Ok(workflows);
    }

    // todo: use the return value of ScheduleWorkflow.Invoke.

    private static async Task<IResult> StartNow([AsParameters] ScheduleRequest parameters, [FromBody] WorkflowStartNowBody body)
    {
        var inMemoryFork = new InMemoryFork();
        using var unsubscribe = parameters.ForkLogging.With(inMemoryFork);
        await parameters.ScheduleWorkflow.AsOnce(body.Pattern, new WorkflowMod
        {
            StartAt = DateTimeOffset.UtcNow,
            Steps = body.Steps
        });
        return Results.Accepted($"/api/workflows", new { });
    }

    private static async Task<IResult> StartIn([AsParameters] ScheduleRequest parameters, [FromBody] WorkflowStartInBody body)
    {
        var inMemoryFork = new InMemoryFork();
        using var unsubscribe = parameters.ForkLogging.With(inMemoryFork);
        await parameters.ScheduleWorkflow.AsOnce(body.Pattern, new WorkflowMod
        {
            StartAt = DateTimeOffset.UtcNow + body.Wait,
            Steps = body.Steps
        });

        return Results.Accepted($"/api/workflows", new { });
    }

    private static async Task<IResult> StartAt([AsParameters] ScheduleRequest parameters, [FromBody] WorkflowStartAtBody body)
    {
        var inMemoryFork = new InMemoryFork();
        using var unsubscribe = parameters.ForkLogging.With(inMemoryFork);
        await parameters.ScheduleWorkflow.AsOnce(body.Pattern, new WorkflowMod
        {
            StartAt = body.WhenUtc,
            Steps = body.Steps
        });

        return Results.Accepted("/api/workflows", new { });
    }

    private static async Task<IResult> Synchronize([AsParameters] ScheduleRequest parameters)
    {
        var inMemoryFork = new InMemoryFork();
        using var unsubscribe = parameters.ForkLogging.With(inMemoryFork);

        await parameters.ScheduleWorkflow.AsCron();

        var results =
            inMemoryFork
                .GroupBy
                (
                    e => e.GetScalarOrDefault<string>("workflow.name") ?? "(other)",
                    e => e, (workflow, events) => new { workflow, events = events.Format("[{Level:u3}] {Message:lj}").ToList() }
                )
                .ToList();

        return Results.Ok(results);
    }
}

public struct ScheduleRequest
{
    public ForkLogging ForkLogging { get; init; }
    public ScheduleWorkflow ScheduleWorkflow { get; init; }
}

public record WorkflowQuery
{
    public string Pattern { get; init; } = null!;
}

public record WorkflowStartNowBody : WorkflowQuery
{
    public string[] Steps { get; init; } = [];
}

public record WorkflowStartInBody : WorkflowQuery
{
    public TimeSpan Wait { get; init; }
    public string[] Steps { get; init; } = [];
}

// note: Does not validate the input because the scheduler does that already.
public record WorkflowStartAtBody : WorkflowQuery
{
    public DateTime When { get; init; }
    public string[] Steps { get; init; } = [];

    public DateTimeOffset WhenUtc => When.ToUniversalTime();
}