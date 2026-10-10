using System;
using Aion.Core.Interface.Endpoints;
using Aion.Core.Interface.Jobs;
using Aion.Core.Objective;
using Aion.Core.Objective.Downtimes;
using Aion.Core.Objective.Workflows;
using Aion.Meta;
using Aion.Meta.Serilog;
using Aion.Meta.Serilog.Enriching;
using Aion.Util.Buzz;
using Aion.Util.Data.Scheduler;
using Aion.Util.Data.Scheduler.JobExecutionRules;
using Aion.Util.Data.StepExecutionRules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.AspNetCore;
using Quartz.Impl.Matchers;
using Serilog;
using ExecuteStep = Aion.Core.Objective.Workflows.ExecuteStep;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//builder.Services.AddOptions<>();
builder.Services.ConfigureFromSection<SchedulerOptions>(builder.Configuration);
builder.Services.AddSingleton<IValidateOptions<SchedulerOptions>, SchedulerOptionsValidation>();
builder.Services.AddSingleton<IPostConfigureOptions<SchedulerOptions>, SchedulerOptionsPostConfigure>();
builder.Services.AddSingleton<MapLogEvent>();

builder
    .Configuration
    .AddJsonFile("appsettings.Serilog.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Profiles.json", optional: false, reloadOnChange: true)
    .AddCommandLine(args);

builder
    .Logging
    .ClearProviders();

builder
    .Host
    .UseSerilog((context, services, configuration) =>
    {
        var schedulerOptions = services.GetRequiredService<IOptions<SchedulerOptions>>().Value;

        // note: Main loggers are filtered in the appsettings.Serilog.json as there is no way to set up these filters here.
        // See https://github.com/serilog/serilog-expressions for all filter expressions.

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.With<EnrichesLogEventWithActivity>()
            .Enrich.FromLogContext()
            //.Enrich.WithProperty("Application", Program.Name)
            //.Enrich.WithProperty("Version", Program.Version)
            .Enrich.WithProperty("Instance", schedulerOptions.Name)
            .Enrich.With(new EnrichesLogEventWithDuration(ts => (int)ts.TotalMilliseconds, "DurationMs"))
            .WriteTo.Sink(services.GetRequiredService<MapLogEvent>());
    });

builder.Services.AddSingleton(x => x.GetRequiredService<IHostEnvironment>().ContentRootFileProvider);

builder.Services.AddScoped<WorkflowExecutionJob>();
builder.Services.AddScoped<WorkflowPollingJob>();


// meta: These two services must be singletons because otherwise cannot inject them into ITriggerListener instances.
builder.Services.AddSingleton<FindWorkflows>();
builder.Services.AddSingleton<GetWorkflow>();
builder.Services.AddSingleton<GetProfile>();
builder.Services.AddSingleton<GetLoggingPreset>();

builder.Services.AddSingleton<DeserializeLoggingPreset>();
builder.Services.AddSingleton<DeserializeWorkflowProfile>();
builder.Services.AddSingleton<DeserializeWorkflowSpec>();

builder.Services.AddScoped<CreateWorkflow>();
builder.Services.AddScoped<ExecuteWorkflow>();
builder.Services.AddScoped<ExecuteStep>();
builder.Services.AddScoped<IStepExecutionRule, StepMustBeEnabled>();
builder.Services.AddScoped<IStepExecutionRule, StepDependsOnPrevious>();
builder.Services.AddScoped<StartProcess>();
builder.Services.AddScoped<WorkflowSynchronizationMustBeEnabled>();
builder.Services.AddScoped<WorkflowCannotExecuteDuringDowntime>();

builder.Services.AddQuartz(configure =>
{
    configure.AddTriggerListener<WorkflowSynchronizationMustBeEnabled>(GroupMatcher<TriggerKey>.GroupStartsWith(nameof(WorkflowPollingJob)));
    configure.AddTriggerListener<WorkflowCannotExecuteDuringDowntime>(GroupMatcher<TriggerKey>.GroupStartsWith(nameof(WorkflowExecutionJob)));

    // note: The docs say that the default is 1 minute.
    configure.MisfireThreshold = TimeSpan.FromMinutes(2);
});


builder.Services.AddQuartzServer(options =>
{
    var schedulerOptions = builder.Configuration.GetRequiredSection("Scheduler").Get<SchedulerOptions>()!;

    options.AwaitApplicationStarted = true;
    options.WaitForJobsToComplete = true;
    options.StartDelay = schedulerOptions.StartDelay;
});


builder.Services.AddScoped<GetWorkflowInfo>();
builder.Services.AddScoped<ScheduleWorkflow>();
builder.Services.AddScoped<SynchronizeWorkflows>();
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
    // todo: create the profile sync-job.
    var scheduleWorkflowPolling = scope.ServiceProvider.GetRequiredService<ScheduleWorkflowPolling>();
    await scheduleWorkflowPolling.Invoke();
}

app.Run();