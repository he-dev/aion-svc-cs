using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Aion.DependencyInjection;
using Aion.Jobs;
using Aion.Services;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;

namespace Aion;

internal static class Program
{
    private const string Name = "Aion";
    private const string Version = "5.0.0";

    private static async Task Main(params string[] args)
    {
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        using var host =
            Host
                .CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    builder
                        .SetBasePath(Directory.GetCurrentDirectory())
                        //.AddJsonFile("appsettings.json", optional: false)
                        .AddEnvironmentVariables();
                })
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureServices((context, services) =>
                {
                    services.AddQuartz(q =>
                    {
                        //q.UseMicrosoftDependencyInjectionJobFactory();
                        //q.ScheduleJob<WorkflowScheduler>(trigger => { trigger.WithIdentity(nameof(WorkflowScheduler)).WithCronSchedule(schedulerOptions["Schedule"]); });
                    });
                    services.AddHostedService<WorkflowService>();
                    services.Configure<WorkflowService.Options>(context.Configuration.GetSection(nameof(WorkflowService)));
                }).ConfigureContainer<ContainerBuilder>(builder =>
                {
                    builder.RegisterInstance(new StdSchedulerFactory(new NameValueCollection())).As<ISchedulerFactory>().SingleInstance();
                    builder.RegisterType<AutofacJobFactory>().As<IJobFactory>().SingleInstance();
                    builder.RegisterType<WorkflowReader>().SingleInstance();
                    //builder.RegisterType<WorkflowScheduler>();
                    //builder.RegisterType<RobotLauncher>();
                })
                .UseWindowsService(options => { options.ServiceName = $"{Name}-v{Version}"; })
                .Build();

        await host.RunAsync();
    }
}