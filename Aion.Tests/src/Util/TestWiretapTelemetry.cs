using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Magic.Workflows;
using Aion.Data;
using Aion.Util;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Xunit;

namespace Aion.Tests.Util;

public class TestWiretapTelemetry
{
    [Theory]
    [InlineData(0, LogLevel.Debug)]
    [InlineData(0, LogLevel.Information)]
    [InlineData(3, LogLevel.Debug)]
    [InlineData(3, LogLevel.Information)]
    [InlineData(null, LogLevel.Debug)]
    [InlineData(null, LogLevel.Information)]
    public async Task StepEmitsTerminalActivityWithChannelAndDuration(int? exitCode, LogLevel minimumLevel)
    {
        var logs = new InMemoryFork();
        var forkLogging = new ForkLogging();
        using var subscription = forkLogging.Into(logs);
        using var serilog = new LoggerConfiguration().MinimumLevel.Verbose()
            .Enrich.FromLogContext().WriteTo.Sink(forkLogging).CreateLogger();
        using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(minimumLevel).AddSerilog(serilog));
        var executeStep = new ExecuteStep(factory.CreateLogger<ExecuteStep>(), new StartProcess(factory.CreateLogger<StartProcess>()));

        var fileName = exitCode is null
            ? Path.Join(Path.GetTempPath(), $"missing-wiretap-test-{Guid.NewGuid():N}")
            : OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("ComSpec")! : "/bin/sh";
        var arguments = OperatingSystem.IsWindows() ? $"/d /c exit {exitCode}" : $"-c \"exit {exitCode}\"";
        var spec = new WorkflowSpec.Step
        {
            Index = 0,
            FileName = fileName,
            Arguments = [arguments],
            Environment = ImmutableDictionary<string, string>.Empty,
            WorkingDirectory = Path.GetTempPath(),
            Timeout = TimeSpan.FromSeconds(10)
        };
        var step = new Workflow.Step
        {
            Spec = spec,
            Index = spec.Index,
            Enabled = true,
            FileName = spec.FileName,
            Arguments = _ => spec.Arguments,
            Environment = spec.Environment,
            WorkingDirectory = spec.WorkingDirectory,
            Timeout = spec.Timeout
        };

        var succeeded = await executeStep.AsProcess(step);

        Assert.Equal(exitCode == 0, succeeded);
        var terminal = Assert.Single(logs, log => log.Properties.TryGetValue("activity.event", out var value) && value.ToString().Trim('"') == "Last");
        Assert.Equal("ExecuteStep", terminal.GetScalarOrDefault<string>("activity.about"));
        Assert.Equal(exitCode == 0 ? "Internal" : "Official", terminal.GetScalarOrDefault<string>("activity.channel"));
        Assert.Equal(exitCode == 0 ? LogEventLevel.Information : LogEventLevel.Error, terminal.Level);
        var duration = Assert.IsType<ScalarValue>(terminal.Properties["activity.duration_ms"]);
        Assert.True(Assert.IsType<long>(duration.Value) >= 0);
        Assert.DoesNotContain("{activity.duration_ms", terminal.RenderMessage());
        Assert.DoesNotContain(logs, log => log.Properties.Keys.Any(key => key.StartsWith("scene.", StringComparison.Ordinal)));
        Assert.DoesNotContain(logs, log => log.Properties.ContainsKey("context.beep_code"));
    }
}
