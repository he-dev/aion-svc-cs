using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.Core.Objective;
using Aion.Core.Objective.Workflows;
using Aion.Util.Buzz;
using Aion.Util.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

    private static async Task<IResult> GetWorkflows(ComposeWorkflowInfo composeWorkflowInfo, [FromBody] WorkflowQuery? body)
    {
        var workflows = await composeWorkflowInfo.Where(body?.Pattern);
        return Results.Ok(workflows);
    }

    // todo: use the return value of ScheduleWorkflow.Invoke.

    private static async Task<IResult> StartNow([AsParameters] StartRequest parameters, [FromBody] WorkflowQuery body)
    {
        var schedules = new List<object>();
        foreach (var workflowPath in parameters.FindWorkflows.Where(body.Pattern))
        {
            var trigger = CreateWorkflowTrigger.Simple(workflowPath, DateTimeOffset.UtcNow);
            var workflow = await parameters.ComposeWorkflow.From(workflowPath, new WorkflowPatch { Trigger = trigger });
            var progress = new ScheduleWorkflowProgress();
            var schedule = await parameters.ScheduleWorkflow.Invoke(workflow, progress);
            schedules.Add(new { workflow.Path.Workflow, schedule });
        }

        return Results.Accepted($"/api/workflows", new { schedules });
    }

    private static async Task<IResult> StartIn([AsParameters] StartRequest parameters, [FromBody] WorkflowStartInBody body)
    {
        var schedules = new List<object>();
        foreach (var workflowPath in parameters.FindWorkflows.Where(body.Pattern))
        {
            var trigger = CreateWorkflowTrigger.Simple(workflowPath, DateTimeOffset.UtcNow + body.Wait);
            var workflow = await parameters.ComposeWorkflow.From(workflowPath, new WorkflowPatch { Trigger = trigger });
            var progress = new ScheduleWorkflowProgress();
            var schedule = await parameters.ScheduleWorkflow.Invoke(workflow, progress);
            schedules.Add(new { workflow.Path.Workflow, schedule });
        }

        return Results.Accepted($"/api/workflows", new { schedules });
    }

    private static async Task<IResult> StartAt([AsParameters] StartRequest parameters, [FromBody] WorkflowStartAtBody body)
    {
        var schedules = new List<object>();
        foreach (var workflowPath in parameters.FindWorkflows.Where(body.Pattern))
        {
            var trigger = CreateWorkflowTrigger.Simple(workflowPath, body.WhenUtc);
            var workflow = await parameters.ComposeWorkflow.From(workflowPath, new WorkflowPatch { Trigger = trigger });
            var progress = new ScheduleWorkflowProgress();
            var schedule = await parameters.ScheduleWorkflow.Invoke(workflow, progress);
            schedules.Add(new { workflow.Path.Workflow, schedule });
        }

        return Results.Accepted($"/api/workflows", new { schedules });
    }

    private static async Task<IResult> Synchronize(SynchronizeWorkflows synchronizeWorkflows)
    {
        var syncResults = await synchronizeWorkflows.Invoke();
        return Results.Ok(syncResults);
    }
}

public struct StartRequest
{
    public FindWorkflows FindWorkflows { get; init; }
    public ComposeWorkflow ComposeWorkflow { get; init; }
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