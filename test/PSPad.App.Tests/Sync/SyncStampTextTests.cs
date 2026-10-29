using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.App.Sync;
using PSPad.App.Tests.Localization;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class SyncStampTextTests
{
    static readonly IStringLocalizer<ShellStrings> Text = TestLocalizers.For<ShellStrings>();
    static readonly DateTimeOffset Noon = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ItSaysWhenTheDataLastArrived() =>
        Assert.Equal(("Updated 2 min ago", false), Describe(new Status(LastSyncedAt: Noon.AddMinutes(-2))));

    [Fact]
    public void ItSaysSoBeforeTheFirstSync() =>
        Assert.Equal(("Not synced yet", false), Describe(new Status()));

    [Fact]
    public void ItSaysUpdatingWhileASyncRuns() =>
        Assert.Equal(("Updating…", false), Describe(new Status(IsSyncing: true, LastSyncedAt: Noon.AddMinutes(-2))));

    [Fact]
    public void OfflineItKeepsTheLastTimeVisible() =>
        Assert.Equal(
            ("Offline · updated 3 days ago", false),
            Describe(new Status(LastSyncedAt: Noon.AddDays(-3)), online: false));

    [Fact]
    public void OfflineBeforeTheFirstSyncItSaysOffline() =>
        Assert.Equal(("Offline", false), Describe(new Status(), online: false));

    [Fact]
    public void AFailedSyncSaysSoAndKeepsTheLastGoodTime() =>
        Assert.Equal(
            ("Couldn't update · updated 5 min ago", true),
            Describe(new Status(LastSyncFailed: true, LastSyncedAt: Noon.AddMinutes(-5))));

    [Fact]
    public void AFailedFirstSyncSaysItCouldNotUpdate() =>
        Assert.Equal(("Couldn't update", true), Describe(new Status(LastSyncFailed: true)));

    [Fact]
    public void AFailureSeenOfflineReadsAsOffline() =>
        Assert.Equal(
            ("Offline · updated 5 min ago", false),
            Describe(new Status(LastSyncFailed: true, LastSyncedAt: Noon.AddMinutes(-5)), online: false));

    static (string Text, bool Failed) Describe(ISyncStatus status, bool online = true) =>
        SyncStampText.Describe(status, online, Noon, Text);

    sealed record Status(bool IsSyncing = false, bool LastSyncFailed = false, DateTimeOffset? LastSyncedAt = null)
        : ISyncStatus
    {
        public IReadOnlyList<string> LastRejections => [];

        public bool HasStuckChange => false;

        public Task DiscardStuckChangeAsync() => Task.CompletedTask;

#pragma warning disable CS0067
        public event Action? Changed;
#pragma warning restore CS0067
    }
}
