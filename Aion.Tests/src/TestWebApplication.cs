using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace Aion.Tests;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TestWebApplication : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // meta: Removes the Quartz-Hosted-Service to prevent triggers from firing during tests.
            var quartzHostedService = services.Single
            (d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType == typeof(QuartzHostedService)
            );
            services.Remove(quartzHostedService);
        });
    }
}