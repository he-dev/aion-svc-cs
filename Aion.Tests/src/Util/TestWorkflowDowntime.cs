using System;
using System.IO;
using System.Threading.Tasks;
using Aion.Util;
using Aion.Util.Data;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aion.Tests.Util;

public class TestWorkflowDowntime
{
    [Fact]
    public void IsPendingWhenStartBeforeNow()
    {
        // core: The lock starts in an hour from the fake now.
        var fakeNowUtc = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 13, 0, 0, TimeSpan.Zero));
        var fakeStartsOnUtcNow = new DateTimeOffset(2025, 1, 1, 14, 0, 0, TimeSpan.Zero);
        var fakeEndsOnUtcNow = new DateTimeOffset(2025, 1, 1, 15, 0, 0, TimeSpan.Zero);

        var workflowDowntime = WorkflowDowntime.At(fakeStartsOnUtcNow, fakeEndsOnUtcNow, fakeNowUtc) with { Clock = fakeNowUtc };

        Assert.Equal(WorkflowDowntimeStatus.Pending, workflowDowntime.Status);
        Assert.Equal(TimeSpan.FromHours(2), workflowDowntime.Remaining);
        Assert.Equal(TimeSpan.FromHours(1), workflowDowntime.Duration);
    }

    [Fact]
    public void IsRunningWhenNowBetweenStartAndEnd()
    {
        // core: The fake now is between start and end.
        var fakeStartsOnUtcNow = new DateTimeOffset(2025, 1, 1, 13, 0, 0, TimeSpan.Zero);
        var fakeNowUtc = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 14, 0, 0, TimeSpan.Zero));
        var fakeEndsOnUtcNow = new DateTimeOffset(2025, 1, 1, 15, 0, 0, TimeSpan.Zero);

        var workflowDowntime = WorkflowDowntime.At(fakeStartsOnUtcNow, fakeEndsOnUtcNow, fakeNowUtc) with { Clock = fakeNowUtc };

        Assert.Equal(WorkflowDowntimeStatus.Ongoing, workflowDowntime.Status);
        Assert.Equal(TimeSpan.FromHours(1), workflowDowntime.Remaining);
        Assert.Equal(TimeSpan.FromHours(2), workflowDowntime.Duration);
    }

    [Fact]
    public void IsExpiredWhenEndAfterNow()
    {
        // core: The fake now is after end.
        var fakeStartsOnUtcNow = new DateTimeOffset(2025, 1, 1, 13, 0, 0, TimeSpan.Zero);
        var fakeEndsOnUtcNow = new DateTimeOffset(2025, 1, 1, 14, 0, 0, TimeSpan.Zero);
        var fakeUtcNow = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 13, 30, 0, TimeSpan.Zero));
        var fakeUtcLater = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 15, 0, 0, TimeSpan.Zero));

        var workflowDowntime = WorkflowDowntime.At(fakeStartsOnUtcNow, fakeEndsOnUtcNow, fakeUtcNow) with { Clock = fakeUtcLater };

        Assert.Equal(WorkflowDowntimeStatus.Expired, workflowDowntime.Status);
        Assert.Equal(TimeSpan.FromHours(-1), workflowDowntime.Remaining);
        Assert.Equal(TimeSpan.FromHours(1), workflowDowntime.Duration);
    }

    [Fact]
    public async Task ThrowsWhenDeletingOfNotSavedLock()
    {
        // core: The lock starts in an hour from the fake now.
        var fakeNowUtc = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 13, 0, 0, TimeSpan.Zero));
        var fakeStartsOnUtcNow = new DateTimeOffset(2025, 1, 1, 14, 0, 0, TimeSpan.Zero);
        var fakeEndsOnUtcNow = new DateTimeOffset(2025, 1, 1, 15, 0, 0, TimeSpan.Zero);

        var workflowLock = WorkflowDowntime.At(fakeStartsOnUtcNow, fakeEndsOnUtcNow, fakeNowUtc) with { Clock = fakeNowUtc };

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await workflowLock.EndsNow());
    }

    [Fact]
    public async Task CanSaveAndDeleteLock()
    {
        // core: The lock starts in an hour from the fake now.
        var fakeNowUtc = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 13, 0, 0, TimeSpan.Zero));
        var fakeStartsOnUtcNow = new DateTimeOffset(2025, 1, 1, 14, 0, 0, TimeSpan.Zero);
        var fakeEndsOnUtcNow = new DateTimeOffset(2025, 1, 1, 15, 0, 0, TimeSpan.Zero);

        var workflowDowntime = WorkflowDowntime.At(fakeStartsOnUtcNow, fakeEndsOnUtcNow, fakeNowUtc) with { Clock = fakeNowUtc };
        var lockPath = await workflowDowntime.ToFile(@"workflows\says-hallo.json");

        Assert.True(File.Exists(lockPath));

        workflowDowntime = await WorkflowDowntime.FromFile(lockPath);
        workflowDowntime = workflowDowntime! with { Clock = fakeNowUtc };

        Assert.Equal(WorkflowDowntimeStatus.Pending, workflowDowntime.Status);
        Assert.Equal(TimeSpan.FromHours(2), workflowDowntime.Remaining);
        Assert.Equal(TimeSpan.FromHours(1), workflowDowntime.Duration);

        await workflowDowntime.EndsNow();

        Assert.False(File.Exists(lockPath));
    }
}