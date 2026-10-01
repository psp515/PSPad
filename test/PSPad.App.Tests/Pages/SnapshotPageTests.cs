using System.Net;
using System.Net.Http.Json;
using AngleSharp.Html.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Api;
using PSPad.App.Auth;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.Tests.Auth;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class SnapshotPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);
    static readonly Guid TaskId = Guid.NewGuid();
    static readonly Guid StepId = Guid.NewGuid();

    FakeHandler _handler = null!;
    RecordingSnapshotsApi _api = null!;
    ISnapshotCache _cache = null!;

    [Fact]
    public void ItRendersTasksAndSteps()
    {
        Arrange(SampleTaskSnapshot());

        var page = RenderPage("tok123");

        Assert.Contains("Renew the domain", page.Markup);
        Assert.Contains("Buy stamps", page.Markup);
        Assert.Contains("Weekly list", page.Markup);
    }

    [Fact]
    public void ItRendersReferenceItemsAndFields()
    {
        Arrange(SampleReferenceSnapshot());

        var page = RenderPage("tok123");

        Assert.Contains("PLA filament", page.Markup);
        Assert.Contains("Brand", page.Markup);
    }

    [Fact]
    public void ADoneTaskIsStruckThrough()
    {
        Arrange(SampleTaskSnapshot());

        var page = RenderPage("tok123");

        var name = page.FindAll(".pspad-snapshot-task .mud-typography-body1")
            .First(element => element.TextContent.Contains("Renew the domain"));
        Assert.Contains("line-through", name.GetAttribute("style"));
    }

    [Fact]
    public void TickingATaskMarksItAndCallsMarkAsync()
    {
        Arrange(SampleTaskSnapshot());
        var page = RenderPage("tok123");

        var checkbox = page.Find("input.mud-checkbox-input");
        checkbox.Change(true);

        Assert.Contains(_handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.True(((IHtmlInputElement)page.Find("input.mud-checkbox-input")).IsChecked);
    }

    [Fact]
    public async Task AFailedTickRevertsAndShowsASnackbar()
    {
        Arrange(SampleTaskSnapshot(), markSucceeds: false);
        var page = RenderPage("tok123");

        page.Find("input.mud-checkbox-input").Change(true);

        var snackbar = page.Services.GetRequiredService<ISnackbar>();
        Assert.Contains(
            snackbar.ShownSnackbars, snack => snack.Message?.Contains("Couldn't save that tick. Try again.") == true);

        _handler.MarkSucceeds = true;
        page.Find("input.mud-checkbox-input").Change(true);

        var secondMark = _handler.Requests.Last(request => request.Method == HttpMethod.Post);
        var body = await secondMark.Content!.ReadFromJsonAsync<MarkSnapshotEntryRequest>(
            Xunit.TestContext.Current.CancellationToken);
        Assert.True(body!.Marked);
    }

    [Fact]
    public void AnUnknownTokenShowsTheExpiredScreen()
    {
        Arrange(snapshot: null);

        var page = RenderPage("deadtoken");

        var empty = page.FindComponent<EmptyState>();
        Assert.Equal("This snapshot has expired or never existed.", empty.Instance.Message);
        Assert.Contains("What is PSPad?", page.Markup);
    }

    [Fact]
    public async Task OfflineWithACachedCopyShowsABannerAndDisabledBoxes()
    {
        var snapshot = SampleTaskSnapshot();
        Arrange(snapshot, throwOnGet: true);
        await _cache.SaveAsync("tok123", snapshot);

        var page = RenderPage("tok123");

        Assert.Contains("Offline", page.Markup);
        var checkbox = (IHtmlInputElement)page.Find("input.mud-checkbox-input");
        Assert.True(checkbox.IsDisabled);
    }

    [Fact]
    public void OfflineWithNoCachedCopyAsksToConnect()
    {
        Arrange(snapshot: null, throwOnGet: true);

        var page = RenderPage("tok123");

        var empty = page.FindComponent<EmptyState>();
        Assert.Equal("Connect to the internet to open this snapshot.", empty.Instance.Message);
    }

    [Fact]
    public async Task ASignedInVisitorRecordsTheVisitAndCachesTheSnapshot()
    {
        Arrange(SampleTaskSnapshot(), session: SampleSession());

        RenderPage("tok123");

        Assert.Contains("tok123", _api.Visited);
        Assert.NotNull(await _cache.GetAsync("tok123"));
    }

    [Fact]
    public async Task AnAnonymousVisitorNeitherRecordsNorCaches()
    {
        Arrange(SampleTaskSnapshot());

        RenderPage("tok123");

        Assert.Empty(_api.Visited);
        Assert.Null(await _cache.GetAsync("tok123"));
    }

    IRenderedComponent<SnapshotPage> RenderPage(string token) =>
        Render<SnapshotPage>(parameters => parameters.Add(p => p.Token, token));

    void Arrange(
        SnapshotView? snapshot,
        bool throwOnGet = false,
        bool markSucceeds = true,
        LocalSession? session = null)
    {
        AppTestHost.Arrange(this, User, Today);

        _handler = new FakeHandler(snapshot, throwOnGet, markSucceeds);
        Services.AddSingleton(new PublicSnapshotsClient(
            new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") }));

        _api = new RecordingSnapshotsApi();
        Services.AddSingleton<ISnapshotsApi>(_api);

        Services.AddSingleton<ILocalSessionStore>(new InMemoryLocalSessionStore(session));
        Services.AddSingleton<LocalAuthenticationStateProvider>();

        _cache = Services.GetRequiredService<ISnapshotCache>();
    }

    static SnapshotView SampleTaskSnapshot() =>
        new(
            Guid.NewGuid(),
            "Weekly list",
            "Tasks",
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero),
            [
                new SnapshotTaskView(
                    TaskId, "Renew the domain", true, null, "None", false, "", false, null,
                    [new SnapshotStepView(StepId, "Buy stamps", false, false, null)])
            ],
            []);

    static SnapshotView SampleReferenceSnapshot() =>
        new(
            Guid.NewGuid(),
            "Print supplies",
            "Reference",
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero),
            [],
            [
                new SnapshotItemView(
                    Guid.NewGuid(), "PLA filament", "", false, false, null,
                    [new SnapshotFieldView("Brand", "Prusament", null)])
            ]);

    static LocalSession SampleSession() =>
        new(User, "Zoe", "zoe@example.com", "UTC", "refresh", DateTimeOffset.UtcNow);

    sealed class FakeHandler(SnapshotView? snapshot, bool throwOnGet, bool markSucceeds) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public bool MarkSucceeds { get; set; } = markSucceeds;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (request.Method == HttpMethod.Get)
            {
                if (throwOnGet)
                {
                    throw new HttpRequestException("offline");
                }

                return Task.FromResult(snapshot is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(snapshot) });
            }

            return Task.FromResult(new HttpResponseMessage(
                MarkSucceeds ? HttpStatusCode.NoContent : HttpStatusCode.BadRequest));
        }
    }

    sealed class RecordingSnapshotsApi : ISnapshotsApi
    {
        public List<string> Visited { get; } = [];

        public Task<PublishedSnapshotView?> PublishAsync(Guid listId, DateTimeOffset expiresAt) =>
            Task.FromResult<PublishedSnapshotView?>(null);

        public Task<IReadOnlyList<PublishedSnapshotView>> ForListAsync(Guid listId) =>
            Task.FromResult<IReadOnlyList<PublishedSnapshotView>>([]);

        public Task<bool> RevokeAsync(Guid snapshotId) => Task.FromResult(false);

        public Task<bool> RecordVisitAsync(string token)
        {
            Visited.Add(token);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<SnapshotVisitView>> VisitsAsync() =>
            Task.FromResult<IReadOnlyList<SnapshotVisitView>>([]);
    }
}
