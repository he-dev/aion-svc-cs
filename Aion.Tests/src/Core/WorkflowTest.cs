using System;
using System.IO;
using System.Threading.Tasks;
using Aion.Util;
using Aion.Util.Data;
using Xunit;

namespace Aion.Tests.Core;

public class WorkflowTest
{
    [Fact]
    public async Task ThrowsWhenFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => WorkflowSpec.FromFile(@"C:\fake\path\to", "workflow.json"));
    }

    [Fact]
    public async Task ThrowsWhenFileNameContainsInvalidCharacters()
    {
        //await Assert.ThrowsAsync<WorkflowNameNotUrlSafeException>(() => WorkflowTemplate.FromFile(@"workflows\invalid\contains-illegal-chars-$.json"));
    }

    [Fact]
    public async Task ThrowsWhenTemplatesUseUndefinedVariables()
    {
        var ex = await Assert.ThrowsAsync<Scriban.Syntax.ScriptRuntimeException>(() => WorkflowSpec.FromFile(@"workflows", @"\invalid\uses-undefined-vars.json"));
        Assert.Equal("<input>(1,8) : error : Variable 'fakeVar' not found.", ex.Message);
    }

    [Fact]
    public async Task ThrowsWhenTemplatesUseUndefinedEnvironmentVariables()
    {
        var ex = await Assert.ThrowsAsync<Scriban.Syntax.ScriptRuntimeException>(() => WorkflowSpec.FromFile(@"workflows", @"\invalid\uses-undefined-env.json"));
        Assert.Equal("<input>(1,4) : error : Environment variable 'FAKE_VAR' not defined.", ex.Message);
    }

    [Fact]
    public async Task ThrowsWhenCronIsInvalid()
    {
        await Assert.ThrowsAsync<FormatException>(() => WorkflowSpec.FromFile(@"workflows", @"\invalid\uses-invalid-cron.json"));
    }

    [Fact]
    public async Task CanReadFromJson()
    {
        var workflow = await WorkflowSpec.FromFile(@"workflows", @"\says-hallo.json");
        //Assert.Equal("says-hallo", workflow.Name);
        Assert.Equal(true, workflow.Enabled);
        Assert.Equal("0/15 * * * * ?", workflow.Cron);
        Assert.Equal(1, workflow.Steps.Length);
    }
}