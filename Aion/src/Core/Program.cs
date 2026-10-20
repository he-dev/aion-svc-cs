using System;
using Aion.Core.Interface.Endpoints;
using Aion.Core.Interface.Jobs;
using Aion.Core.Objective.Downtimes;
using Aion.Core.Objective.Workflows;
using Aion.Meta;
using Aion.Meta.Serilog;
using Aion.Meta.Serilog.Enriching;
using Aion.Util.Buzz;
using Aion.Util.Buzz.TriggerListeners;
using Aion.Util.Data.Scheduler;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Serilog;
using ExecuteStep = Aion.Core.Objective.Workflows.ExecuteStep;

var builder = WebApplication.CreateBuilder(args);

const string WorkflowLogKey = "WorkflowLog";


// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//builder.Services.AddOptions<>();
builder.Services.ConfigureFromSection<SchedulerOptions>(builder.Configuration);
builder.Services.AddSingleton<IValidateOptions<SchedulerOptions>, SchedulerOptionsValidation>();
builder.Services.AddSingleton<IPostConfigureOptions<SchedulerOptions>, SchedulerOptionsPostConfigure>();
builder.Services.AddKeyedSingleton(WorkflowLogKey, new MapLogEventByProperty("aion.workflow.execution_id"));
builder.Services.AddSingleton<BugLogging>();


builder
    .Configuration
    .AddJsonFile("appsettings.Scheduler.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Serilog.json", optional: true, reloadOnChange: true)
    .AddCommandLine(args);

builder
    .Logging
    .ClearProviders();

builder
    .Host
    .UseSerilog((context, services, configuration) =>
    {
        //var schedulerOptions = services.GetRequiredService<IOptions<SchedulerOptions>>().Value;

        // note: Main loggers are filtered in the appsettings.Serilog.json as there is no way to set up these filters here.
        // See https://github.com/serilog/serilog-expressions for all filter expressions.

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.With<EnrichLogEventWithActivity>()
            .Enrich.With(new EnrichLogEventWithDuration(ts => (int)ts.TotalMilliseconds))
            .WriteTo.Sink(services.GetRequiredKeyedService<MapLogEventByProperty>(WorkflowLogKey))
            .WriteTo.Sink(services.GetRequiredService<BugLogging>());
    });

builder.Services.AddSingleton(x => x.GetRequiredService<IHostEnvironment>().ContentRootFileProvider);

builder.Services.AddScoped<WorkflowExecutionJob>();
builder.Services.AddScoped<WorkflowPollingJob>();
builder.Services.AddScoped<ValidateWorkflowProfile>();
builder.Services.AddScoped<ValidateWorkflowFile>();


// meta: These two services must be singletons because otherwise cannot inject them into ITriggerListener instances.
builder.Services.AddScoped<FindWorkflows>();
builder.Services.AddScoped<ComposeWorkflow>();
builder.Services.AddScoped<LoadWorkflowProfile>();
builder.Services.AddScoped<LoadLoggingPreset>();
builder.Services.AddScoped<LoadLoggingPreset>();
builder.Services.AddScoped<LoadWorkflowProfile>();
builder.Services.AddScoped<LoadWorkflowSpec>();

builder.Services.AddScoped<ComposeWorkflow>();
builder.Services.AddScoped<ExecuteWorkflow>();
builder.Services.AddScoped<ExecuteStep>();
builder.Services.AddScoped<StartProcess>();
builder.Services.AddScoped<WorkflowPollingMustBeEnabled>();
builder.Services.AddScoped<WorkflowCannotExecuteDuringDowntime>();

builder.Services.AddQuartz(configure =>
{
    configure.AddTriggerListener<WorkflowPollingMustBeEnabled>(WorkflowPollingMustBeEnabled.Matchers);
    //configure.AddTriggerListener<WorkflowCannotExecuteDuringDowntime>(GroupMatcher<TriggerKey>.GroupStartsWith(nameof(WorkflowExecutionJob)));

    configure.UseInMemoryStore(options =>
    {
        // note: The docs say that the default is 1 minute.
        options.MisfireThreshold = TimeSpan.FromMinutes(2);
    });
});


builder.Services.AddQuartzHostedService(options =>
{
    var schedulerOptions = builder.Configuration.GetRequiredSection("Scheduler").Get<SchedulerOptions>()!;

    options.AwaitApplicationStarted = true;
    options.WaitForJobsToComplete = true;
    options.StartDelay = schedulerOptions.StartDelay;
});


builder.Services.AddScoped<ComposeWorkflowOverview>();
builder.Services.AddScoped<ScheduleWorkflow>();
builder.Services.AddScoped<ScheduleWorkflowPolling>();
builder.Services.AddScoped<ExecuteWorkflow>();
builder.Services.AddScoped<GetProfileTriggers>();
builder.Services.AddScoped<GetDowntimes>();
builder.Services.AddScoped<StartDowntime>();
builder.Services.AddScoped<EndDowntime>();

builder.Services.AddProblemDetails();
//builder.Services.AddExceptionHandler<WorkflowNotExecutableExceptionHandler>();

var app = builder.Build();

//app.UseExceptionHandler();
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapWorkflows();
app.MapSchedules();
app.MapDowntimes();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ScheduleWorkflowPolling>().Invoke();
}

app.Run();