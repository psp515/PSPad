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

    static readonly DateTimeOffset Midnight = new(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    [Fact]
    public void PublishingWithTheSevenDayPresetListsALinkWithSevenDaysLeft()
    {
        Arrange(new FakeSnapshotsApi());

        var publisher = Render(NewList());
        PublishButton(publisher).Click();

        Assert.Contains("7daysleft", Squash(publisher.Text()));
    }

    [Fact]
    public void PublishingIsDisabledOffline()
    {
        Arrange(new FakeSnapshotsApi(), online: false);

        var publisher = Render(NewList());

        Assert.True(PublishButton(publisher).HasAttribute("disabled"));
        Assert.Contains("Publishing needs a connection.", publisher.Text());
    }

    [Fact]
    public async Task PickingTodayInAWesternZoneExpiresAtThatZonesNextMidnight()
    {
        Arrange(new FakeSnapshotsApi());
        Services.GetRequiredService<AppState>().TimeZone = "America/New_York";

        var publisher = Render(NewList());
        Choose(publisher, "Date…");
        await PickDateAsync(publisher, Today);
        PublishButton(publisher).Click();

        Assert.Contains("2daysleft", Squash(publisher.Text()));
    }

    static Task PickDateAsync(IRenderedComponent<ContainerFragment> publisher, DateOnly date)
    {
        var picker = publisher.FindComponent<MudDatePicker>();
        return publisher.InvokeAsync(() => picker.Instance.DateChanged.InvokeAsync(date.ToDateTime(TimeOnly.MinValue)));
    }

    [Fact]
    public void RevokingAfterConfirmingRemovesTheLink()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "token-1", Midnight, Midnight.AddDays(7)));

        var publisher = RenderWith(api);
        Assert.NotEmpty(publisher.FindAll("button[aria-label='Show QR code']"));
        publisher.FindAll("button").Single(button => button.TextContent.Trim() == "Revoke").Click();
        publisher.FindAll("[role=dialog] button").Last().Click();

        Assert.Empty(publisher.FindAll("button[aria-label='Show QR code']"));
        Assert.Contains("No live links", publisher.Text());
    }

    [Fact]
    public void ALiveLinkShowsDaysLeftAndTicks()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "tok-1", Midnight, Midnight.AddDays(6), Entries: 12, Ticks: 4));

        var text = RenderWith(api).Text();

        Assert.Contains("6daysleft", Squash(text));
        Assert.Contains("4 ticked", text);
        Assert.Contains("12 entries", text);
    }

    [Fact]
    public void ALinkWithNoTicksSaysSo()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "tok-1", Midnight, Midnight.AddHours(5)));

        var text = RenderWith(api).Text();

        Assert.Contains("No ticks yet", text);
        Assert.Contains("1dayleft", Squash(text));
    }

    [Fact]
    public void AFreshSevenDayLinkShowsSevenDaysLeft()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "tok-1", Midnight, Midnight.AddDays(7).AddMinutes(-1)));

        Assert.Contains("7daysleft", Squash(RenderWith(api).Text()));
    }

    [Fact]
    public void AFreshOneDayLinkShowsOneDayLeft()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "tok-1", Midnight, Midnight.AddDays(1).AddMinutes(-1)));

        Assert.Contains("1dayleft", Squash(RenderWith(api).Text()));
    }

    [Fact]
    public async Task APickedDateIsNamedInTheUsersZone()
    {
        Arrange(new FakeSnapshotsApi());
        Services.GetRequiredService<AppState>().TimeZone = "America/New_York";

        var publisher = Render(NewList());
        Choose(publisher, "Date…");
        await PickDateAsync(publisher, Today);

        Assert.Contains("Publish until 12 Sep", PublishButton(publisher).TextContent);
    }

    [Fact]
    public void APresetIsNamedByItsLocalExpiryDate()
    {
        Arrange(new FakeSnapshotsApi());
        Services.GetRequiredService<AppState>().TimeZone = "America/New_York";

        var publisher = Render(NewList());

        Assert.Contains("Publish until 18 Sep", PublishButton(publisher).TextContent);
    }

    [Fact]
    public void ThePublishedDateIsShownInTheUsersZone()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "tok-1", Midnight, Midnight.AddDays(6)));
        Arrange(api);
        Services.GetRequiredService<AppState>().TimeZone = "America/New_York";

        var publisher = Render(NewList());

        Assert.Contains("Published 11 Sep", publisher.Text());
    }

    [Fact]
    public void OfflineTheLiveLinksAreNotShown()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "tok-1", Midnight, Midnight.AddDays(6)));
        Arrange(api, online: false);

        var text = Render(NewList()).Text();

        Assert.Contains("Live links need a connection.", text);
        Assert.DoesNotContain("Published 12 Sep", text);
    }

    [Fact]
    public void AFailedFetchShowsTheUnreachableState()
    {
        Arrange(new FakeSnapshotsApi { ThrowOnForList = true });

        Assert.Contains("Live links need a connection.", Render(NewList()).Text());
    }

    [Fact]
    public void AFailedRevokeWarnsAndKeepsTheLink()
    {
        var api = new FakeSnapshotsApi { ThrowOnRevoke = true };
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "tok-1", Midnight, Midnight.AddDays(6)));

        var publisher = RenderWith(api);
        publisher.FindAll("button").Single(button => button.TextContent.Trim() == "Revoke").Click();
        publisher.FindAll("[role=dialog] button").Last().Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Severity == Severity.Warning);
        Assert.NotEmpty(publisher.FindAll("button[aria-label='Show QR code']"));
    }

    [Fact]
    public void AFailedPublishWarnsAndListsNothing()
    {
        Arrange(new FakeSnapshotsApi { ThrowOnPublish = true });

        var publisher = Render(NewList());
        PublishButton(publisher).Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Severity == Severity.Warning);
        Assert.Contains("No live links", publisher.Text());
    }

    [Fact]
    public void TheNewestLinkShowsItsQrCodeAndTheToggleHidesIt()
    {
        var api = new FakeSnapshotsApi();
        api.Active.Add(new PublishedSnapshotView(Guid.NewGuid(), "tok-1", Midnight, Midnight.AddDays(6)));

        var publisher = RenderWith(api);
        var toggle = publisher.Find("button[aria-label='Show QR code']");
        Assert.Equal("true", toggle.GetAttribute("aria-expanded"));
        Assert.Single(publisher.FindAll("img"));

        toggle.Click();

        Assert.Equal("false", publisher.Find("button[aria-label='Show QR code']").GetAttribute("aria-expanded"));
        Assert.Empty(publisher.FindAll("img"));
    }

    [Fact]
    public void ThePublishButtonNamesTheExpiryDate()
    {
        Arrange(new FakeSnapshotsApi());
        var publisher = Render(NewList());
        Assert.Contains("Publish until 19 Sep", PublishButton(publisher).TextContent);
    }

    static AngleSharp.Dom.IElement PublishButton(IRenderedComponent<ContainerFragment> publisher) =>
        publisher.FindAll("button").Single(button =>
            button.TextContent.Contains("Publish until") || button.TextContent.Contains("Pick a date"));

    static void Choose(IRenderedComponent<ContainerFragment> publisher, string preset) =>
        publisher.FindAll("button").Single(button => button.TextContent.Trim() == preset).Click();

    static string Squash(string text) => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));

    IRenderedComponent<ContainerFragment> RenderWith(FakeSnapshotsApi api)
    {
        Arrange(api);
        return Render(NewList());
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

    void Arrange(FakeSnapshotsApi api, bool online = true)
    {
        AppTestHost.Arrange(this, User, Today);
        Services.AddSingleton<ISnapshotsApi>(api);
        Services.AddSingleton<ISyncTrigger>(new QuietSyncTrigger());
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
        public List<PublishedSnapshotView> Active { get; } = [];

        public bool ThrowOnForList { get; init; }

        public bool ThrowOnPublish { get; init; }

        public bool ThrowOnRevoke { get; init; }

        public Task<PublishedSnapshotView?> PublishAsync(Guid listId, DateTimeOffset expiresAt)
        {
            if (ThrowOnPublish)
            {
                throw new HttpRequestException("offline");
            }

            var view = new PublishedSnapshotView(Guid.NewGuid(), $"token-{Active.Count + 1}", Midnight, expiresAt);
            Active.Add(view);
            return Task.FromResult<PublishedSnapshotView?>(view);
        }

        public Task<IReadOnlyList<PublishedSnapshotView>> ForListAsync(Guid listId)
        {
            if (ThrowOnForList)
            {
                throw new HttpRequestException("offline");
            }

            return Task.FromResult<IReadOnlyList<PublishedSnapshotView>>([.. Active]);
        }

        public Task<bool> RevokeAsync(Guid snapshotId)
        {
            if (ThrowOnRevoke)
            {
                throw new TaskCanceledException("offline");
            }

            Active.RemoveAll(snapshot => snapshot.Id == snapshotId);
            return Task.FromResult(true);
        }

        public Task<bool> RecordVisitAsync(string token) => Task.FromResult(true);

        public Task<IReadOnlyList<SnapshotVisitView>> VisitsAsync() =>
            Task.FromResult<IReadOnlyList<SnapshotVisitView>>([]);
    }

    sealed class QuietSyncTrigger : ISyncTrigger
    {
        public Task SyncNowAsync() => Task.CompletedTask;
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
