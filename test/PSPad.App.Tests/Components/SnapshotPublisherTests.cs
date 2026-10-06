using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.Components;
using PSPad.App.State;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class SnapshotPublisherTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void PublishingWithASevenDayPresetSendsAfterSyncing()
    {
        var api = new FakeSnapshotsApi();
        var sync = new RecordingSyncTrigger();
        Arrange(api, sync);
        var list = NewList();

        var publisher = Render(list);
        publisher.Find(".pspad-snapshot-publish").Click();

        Assert.True(sync.Called);
        var (listId, expiresAt) = Assert.Single(api.Published);
        Assert.Equal(list.Id, listId);
        var expectedExpiry = Today.ToDateTime(TimeOnly.MinValue).AddDays(7);
        Assert.True(Math.Abs((expiresAt.UtcDateTime - expectedExpiry).TotalMinutes) < 1);
    }

    [Fact]
    public void PublishingIsDisabledOffline()
    {
        var api = new FakeSnapshotsApi();
        Arrange(api, new RecordingSyncTrigger(), online: false);
        var list = NewList();

        var publisher = Render(list);

        Assert.True(publisher.Find(".pspad-snapshot-publish").HasAttribute("disabled"));
        Assert.Contains("Publishing needs a connection.", publisher.Markup);
    }

    [Fact]
    public async Task PickingTodayInAPositiveOffsetZoneExpiresAtNextLocalMidnightUtc()
    {
        var api = new FakeSnapshotsApi();
        Arrange(api, new RecordingSyncTrigger());
        Services.GetRequiredService<AppState>().TimeZone = "Europe/Warsaw";
        var list = NewList();

        var publisher = Render(list);
        publisher.Find(".pspad-snapshot-preset-date").Click();
        await PickDateAsync(publisher, Today);
        publisher.Find(".pspad-snapshot-publish").Click();

        var (_, expiresAt) = Assert.Single(api.Published);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        var expectedLocalMidnight = Today.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var expectedUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(expectedLocalMidnight, DateTimeKind.Unspecified), zone);
        Assert.Equal(expectedUtc, expiresAt.UtcDateTime);
        Assert.True(expiresAt > Services.GetRequiredService<IClock>().UtcNow);
    }

    static Task PickDateAsync(IRenderedComponent<ContainerFragment> publisher, DateOnly date)
    {
        var picker = publisher.FindComponent<MudDatePicker>();
        return publisher.InvokeAsync(() => picker.Instance.DateChanged.InvokeAsync(date.ToDateTime(TimeOnly.MinValue)));
    }

    [Fact]
    public void RevokingAfterConfirmingCallsRevoke()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(
            Guid.NewGuid(), "token-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7)));
        Arrange(api, new RecordingSyncTrigger());
        var list = NewList();

        var publisher = Render(list);
        publisher.Find(".pspad-snapshot-revoke").Click();
        publisher.FindAll("div.mud-dialog button").Last().Click();

        Assert.Single(api.Revoked);
    }

    static readonly DateTimeOffset Midnight = new(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    [Fact]
    public void ALiveLinkShowsDaysLeftAndTicks()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "tok-1", Midnight, Midnight.AddDays(6), Entries: 12, Ticks: 4));
        Arrange(api, new RecordingSyncTrigger());

        var publisher = Render(NewList());

        Assert.Contains("6", publisher.Find(".pspad-snapshot-days").TextContent);
        Assert.Contains("4 ticked", publisher.Find(".pspad-snapshot-ticks").TextContent);
        Assert.Contains("12 entries", publisher.Markup);
    }

    [Fact]
    public void ALinkWithNoTicksSaysSo()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "tok-1", Midnight, Midnight.AddHours(5)));
        Arrange(api, new RecordingSyncTrigger());

        var publisher = Render(NewList());

        Assert.Contains("No ticks yet", publisher.Find(".pspad-snapshot-ticks").TextContent);
        Assert.Contains("<1", publisher.Find(".pspad-snapshot-days").TextContent);
    }

    [Fact]
    public void TheNewestLinkShowsItsQrCodeAndTheToggleHidesIt()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "tok-1", Midnight, Midnight.AddDays(6)));
        Arrange(api, new RecordingSyncTrigger());

        var publisher = Render(NewList());
        Assert.Single(publisher.FindAll("img.pspad-qr"));

        publisher.Find(".pspad-snapshot-qr-toggle").Click();
        Assert.Empty(publisher.FindAll("img.pspad-qr"));
    }

    [Fact]
    public void ThePublishButtonNamesTheExpiryDate()
    {
        Arrange(new FakeSnapshotsApi(), new RecordingSyncTrigger());
        var publisher = Render(NewList());
        Assert.Contains("Publish until 19 Sep", publisher.Find(".pspad-snapshot-publish").TextContent);
    }

    IRenderedComponent<ContainerFragment> Render(TaskList list) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<SnapshotPublisher>(2);
        builder.AddAttribute(3, nameof(SnapshotPublisher.List), list);
        builder.CloseComponent();
    });

    void Arrange(FakeSnapshotsApi api, RecordingSyncTrigger trigger, bool online = true)
    {
        AppTestHost.Arrange(this, User, Today);
        Services.AddSingleton<ISnapshotsApi>(api);
        Services.AddSingleton<ISyncTrigger>(trigger);
        Services.AddSingleton<IConnectivity>(new ToggleableConnectivity(online));
    }

    static TaskList NewList()
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Dom", 0),
            DateTimeOffset.UnixEpoch));

        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), area.Id, "Zakupy"),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    public sealed class FakeSnapshotsApi : ISnapshotsApi
    {
        public List<(Guid ListId, DateTimeOffset ExpiresAt)> Published { get; } = [];

        public List<Guid> Revoked { get; } = [];

        public List<PublishedSnapshotView> Active { get; } = [];

        public Task<PublishedSnapshotView?> PublishAsync(Guid listId, DateTimeOffset expiresAt)
        {
            Published.Add((listId, expiresAt));
            var view = new PublishedSnapshotView(Guid.NewGuid(), $"token-{Published.Count}", DateTimeOffset.UtcNow, expiresAt);
            Active.Add(view);
            return Task.FromResult<PublishedSnapshotView?>(view);
        }

        public Task<IReadOnlyList<PublishedSnapshotView>> ForListAsync(Guid listId) =>
            Task.FromResult<IReadOnlyList<PublishedSnapshotView>>([.. Active]);

        public Task<bool> RevokeAsync(Guid snapshotId)
        {
            Revoked.Add(snapshotId);
            Active.RemoveAll(snapshot => snapshot.Id == snapshotId);
            return Task.FromResult(true);
        }

        public Task<bool> RecordVisitAsync(string token) => Task.FromResult(true);

        public Task<IReadOnlyList<SnapshotVisitView>> VisitsAsync() =>
            Task.FromResult<IReadOnlyList<SnapshotVisitView>>([]);
    }

    public sealed class RecordingSyncTrigger : ISyncTrigger
    {
        public bool Called { get; private set; }

        public Task SyncNowAsync()
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    sealed class ToggleableConnectivity(bool isOnline) : IConnectivity
    {
        public bool IsOnline { get; set; } = isOnline;

#pragma warning disable CS0067
        public event Action? CameOnline;

        public event Action? Changed;
#pragma warning restore CS0067
    }
}
