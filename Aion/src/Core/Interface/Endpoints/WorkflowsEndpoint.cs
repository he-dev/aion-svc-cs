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

    private static async Task<IResult> StartNow([AsParameters] StartRequest parameters, [FromBody] WorkflowQuery body)
    {
        var changes = new List<object>();
        foreach (var workflowPath in parameters.FindWorkflows.Where(body.Pattern))
        {
            var trigger = CreateWorkflowTrigger.Simple(workflowPath, DateTimeOffset.UtcNow);
            var workflow = await parameters.ComposeWorkflow.From(workflowPath, new WorkflowPatch { Trigger = trigger });
            var change = await parameters.ScheduleWorkflow.At(workflow.Trigger);
            changes.Add(new { workflow.Spec.Path.Workflow, change });
        }

        return Results.Accepted($"/api/workflows", new { changes });
    }

    private static async Task<IResult> StartIn([AsParameters] StartRequest parameters, [FromBody] WorkflowStartInBody body)
    {
        var changes = new List<object>();
        foreach (var workflowPath in parameters.FindWorkflows.Where(body.Pattern))
        {
            var trigger = CreateWorkflowTrigger.Simple(workflowPath, DateTimeOffset.UtcNow + body.Wait);
            var workflow = await parameters.ComposeWorkflow.From(workflowPath, new WorkflowPatch { Trigger = trigger });
            var change = await parameters.ScheduleWorkflow.At(workflow.Trigger);
            changes.Add(new { workflow.Spec.Path.Workflow, change });
        }

        return Results.Accepted($"/api/workflows", new { changes });
    }

    private static async Task<IResult> StartAt([AsParameters] StartRequest parameters, [FromBody] WorkflowStartAtBody body)
    {
        var changes = new List<object>();
        foreach (var workflowPath in parameters.FindWorkflows.Where(body.Pattern))
        {
            var trigger = CreateWorkflowTrigger.Simple(workflowPath, body.WhenUtc);
            var workflow = await parameters.ComposeWorkflow.From(workflowPath, new WorkflowPatch { Trigger = trigger });
            var change = await parameters.ScheduleWorkflow.At(workflow.Trigger);
            changes.Add(new { workflow.Spec.Path.Workflow, change });
        }

        return Results.Accepted($"/api/workflows", new { schedules = changes });
    }

    private static async Task<IResult> Synchronize([AsParameters] SynchronizeRequest parameters)
    {
        using var inMemoryBug = new InMemoryBug(parameters.Logger.BeginScope);
        using var unsubscribe = parameters.BugLogging.With(inMemoryBug);

        await parameters.ScheduleWorkflow.Directory();
        if (inMemoryBug.AtLeastErrors().ToList() is { Count: > 0 } errors)
        {
            return Results.Ok(new { errors = errors.Format("[{Level:u3}] {Message:lj}") });
        }
        else
        {
            return Results.Ok(new { results = inMemoryBug.AtLeastInformations().Messages().ToList() });
        }
    }
}

public struct StartRequest
{
    public FindWorkflows FindWorkflows { get; init; }
    public ComposeWorkflow ComposeWorkflow { get; init; }
    public ScheduleWorkflow ScheduleWorkflow { get; init; }
}

public struct SynchronizeRequest
{
    public ILogger<SynchronizeRequest> Logger { get; init; }
    public BugLogging BugLogging { get; init; }
    public ScheduleWorkflow ScheduleWorkflow { get; init; }
}

public record WorkflowQuery
{
    public string Pattern { get; init; } = null!;
    public string[] Steps { get; init; } = [];
}

public record WorkflowStartInBody : WorkflowQuery
{
    public TimeSpan Wait { get; init; }
}

// note: Does not validate the input because the scheduler does that already.
public record WorkflowStartAtBody : WorkflowQuery
{
    public DateTime When { get; init; }

    public DateTimeOffset WhenUtc => When.ToUniversalTime();
}