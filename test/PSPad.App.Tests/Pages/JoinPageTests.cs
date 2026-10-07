using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Api;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class JoinPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void AValidTokenJoinsAndNavigatesToTheList()
    {
        var listId = Guid.NewGuid();
        var trigger = new RecordingSyncTrigger();
        Arrange(Joined(listId), trigger: trigger);
        NavigateTo("join/abc123#code=K7M4PX");

        var page = RenderPage("abc123");

        var navigation = page.Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/lists/{listId}", navigation.Uri);
        Assert.True(trigger.Called);
    }

    [Fact]
    public void ACodeInTheFragmentJoinsAtOnce()
    {
        var listId = Guid.NewGuid();
        var api = Arrange(Joined(listId));
        NavigateTo("join/tok#code=K7M4PX");

        var page = RenderPage("tok");

        Assert.Equal(("tok", "K7M4PX"), api.Calls.Single());
        Assert.EndsWith($"/lists/{listId}", page.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void WithoutACodeItAsksForOne()
    {
        var api = Arrange(new JoinOutcome.Invalid());
        NavigateTo("join/tok");

        var page = RenderPage("tok");

        Assert.Empty(api.Calls);
        Assert.True(page.Find(".pspad-join-submit").HasAttribute("disabled"));
    }

    [Fact]
    public void TypingAWellFormedCodeEnablesJoinAndSendsItNormalised()
    {
        var api = Arrange(Joined(Guid.NewGuid()));
        NavigateTo("join/tok");
        var page = RenderPage("tok");

        page.Find(".pspad-join-code input").Input("k7m-4px");
        page.Find(".pspad-join-submit").Click();

        Assert.Equal(("tok", "K7M4PX"), api.Calls.Single());
    }

    [Fact]
    public void AnInvalidAnswerShowsOneGenericErrorAndKeepsTheInput()
    {
        Arrange(new JoinOutcome.Invalid());
        NavigateTo("join/tok");
        var page = RenderPage("tok");

        page.Find(".pspad-join-code input").Input("k7m-4px");
        page.Find(".pspad-join-submit").Click();

        Assert.Contains("That link or code doesn't work.", page.Find(".pspad-join-invalid").TextContent);
        Assert.Equal("k7m-4px", page.Find(".pspad-join-code input").GetAttribute("value"));
    }

    [Fact]
    public void AnInvalidFragmentCodeShowsTheErrorAboveTheEntryCard()
    {
        Arrange(new JoinOutcome.Invalid());
        NavigateTo("join/tok#code=K7M4PX");

        var page = RenderPage("tok");

        Assert.Contains("That link or code doesn't work.", page.Find(".pspad-join-invalid").TextContent);
        Assert.NotNull(page.Find(".pspad-join-code"));
    }

    [Fact]
    public void AnExpiredAnswerSaysSoAndOffersMyDay()
    {
        Arrange(new JoinOutcome.Expired());
        NavigateTo("join/tok#code=K7M4PX");

        var page = RenderPage("tok");

        Assert.Contains("This invite has expired", page.Markup);
        Assert.Contains("Invites work for 30 minutes. Ask the owner for a new link and code.", page.Markup);
        page.Find(".pspad-empty-state").Click();
        var navigation = page.Services.GetRequiredService<NavigationManager>();
        Assert.Equal(navigation.BaseUri, navigation.Uri);
    }

    [Fact]
    public void ThrottlingIsExplained()
    {
        Arrange(new JoinOutcome.TooManyTries());
        NavigateTo("join/tok#code=K7M4PX");

        var page = RenderPage("tok");

        Assert.Contains("Too many tries.", page.Find(".pspad-join-throttled").TextContent);
    }

    [Fact]
    public void OfflineOffersARetryThatJoinsOnceBackOnline()
    {
        var listId = Guid.NewGuid();
        var connectivity = new ToggleableConnectivity(isOnline: false);
        Arrange(Joined(listId), connectivity: connectivity);

        NavigateTo("join/abc123#code=K7M4PX");

        var page = RenderPage("abc123");

        var empty = page.FindComponent<EmptyState>();
        Assert.Equal("Joining needs a connection.", empty.Instance.Message);

        connectivity.IsOnline = true;
        page.Find(".pspad-empty-state").Click();

        var navigation = page.Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/lists/{listId}", navigation.Uri);
    }

    [Fact]
    public void ANetworkExceptionBehavesLikeOffline()
    {
        Arrange(new JoinOutcome.Invalid(), throwing: true);

        NavigateTo("join/abc123#code=K7M4PX");

        var page = RenderPage("abc123");

        var empty = page.FindComponent<EmptyState>();
        Assert.Equal("Joining needs a connection.", empty.Instance.Message);
    }

    [Fact]
    public void EnterInTheCodeFieldJoins()
    {
        var api = Arrange(Joined(Guid.NewGuid()));
        NavigateTo("join/tok");
        var page = RenderPage("tok");

        page.Find(".pspad-join-code input").Input("k7m-4px");
        page.Find(".pspad-join-code input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(("tok", "K7M4PX"), api.Calls.Single());
    }

    [Fact]
    public void EnterWithAnIncompleteCodeDoesNothing()
    {
        var api = Arrange(Joined(Guid.NewGuid()));
        NavigateTo("join/tok");
        var page = RenderPage("tok");

        page.Find(".pspad-join-code input").Input("k7m");
        page.Find(".pspad-join-code input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Empty(api.Calls);
    }

    [Fact]
    public void JoiningReplacesTheInviteEntryInHistory()
    {
        var listId = Guid.NewGuid();
        Arrange(Joined(listId));
        NavigateTo("join/tok#code=K7M4PX");

        RenderPage("tok");

        var latest = Navigation.History.First();
        Assert.EndsWith($"/lists/{listId}", latest.Uri);
        Assert.True(latest.Options.ReplaceHistoryEntry);
    }

    [Fact]
    public void AFailedJoinDropsTheCodeFromTheAddressWithoutRetrying()
    {
        var api = Arrange(new JoinOutcome.Invalid());
        NavigateTo("join/tok#code=K7M4PX");

        var page = RenderPage("tok");

        Assert.Equal($"{Navigation.BaseUri}join/tok", Navigation.Uri);
        Assert.True(Navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Single(api.Calls);
        Assert.NotNull(page.Find(".pspad-join-invalid"));
    }

    Bunit.TestDoubles.BunitNavigationManager Navigation =>
        (Bunit.TestDoubles.BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

    void NavigateTo(string uri) => Services.GetRequiredService<NavigationManager>().NavigateTo(uri);

    IRenderedComponent<JoinPage> RenderPage(string token) =>
        Render<JoinPage>(parameters => parameters.Add(p => p.Token, token));

    static JoinOutcome Joined(Guid listId) =>
        new JoinOutcome.Joined(listId, new Dictionary<string, JsonElement[]>());

    FakeSyncApi Arrange(
        JoinOutcome outcome,
        bool throwing = false,
        ToggleableConnectivity? connectivity = null,
        RecordingSyncTrigger? trigger = null)
    {
        var replica = AppTestHost.Arrange(this, User, Today);
        var api = new FakeSyncApi(outcome, throwing);
        var outbox = new InMemoryOutbox();
        Services.AddSingleton<ISyncApi>(api);
        Services.AddSingleton<IOutbox>(outbox);
        Services.AddSingleton(new SyncService(api, replica, outbox));
        Services.AddSingleton<ISyncTrigger>(trigger ?? new RecordingSyncTrigger());
        if (connectivity is not null)
        {
            Services.AddSingleton<IConnectivity>(connectivity);
        }

        return api;
    }

    sealed class FakeSyncApi(JoinOutcome outcome, bool throwing) : ISyncApi
    {
        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes) =>
            Task.FromResult<IReadOnlyList<CommandResponse>>([]);

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full) =>
            Task.FromResult<SyncResponse?>(null);

        public List<(string Token, string Code)> Calls { get; } = [];

        public Task<JoinOutcome> JoinAsync(string token, string code)
        {
            Calls.Add((token, code));
            return throwing ? throw new HttpRequestException("Boom.") : Task.FromResult(outcome);
        }
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
