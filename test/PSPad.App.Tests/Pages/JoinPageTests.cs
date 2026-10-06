using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
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

        var page = RenderPage("abc123");

        var navigation = page.Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/lists/{listId}", navigation.Uri);
        Assert.True(trigger.Called);
    }

    [Fact]
    public void TheCodeInTheFragmentIsSentWithTheToken()
    {
        var api = Arrange(Joined(Guid.NewGuid()));
        Services.GetRequiredService<NavigationManager>().NavigateTo("join/abc123#code=K7M-4PX");

        RenderPage("abc123");

        Assert.Equal(("abc123", "K7M-4PX"), api.JoinedWith);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public void AFailedJoinShowsTheDeadLink(JoinOutcome outcome)
    {
        Arrange(outcome);

        var page = RenderPage("deadtoken");

        Assert.Equal("This invite link no longer works.", page.FindComponent<EmptyState>().Instance.Message);
    }

    public static TheoryData<JoinOutcome> Failures() =>
        [new JoinOutcome.Invalid(), new JoinOutcome.Expired(), new JoinOutcome.TooManyTries()];

    [Fact]
    public void ADeadLinkOffersToGoHome()
    {
        Arrange(new JoinOutcome.Invalid());

        var page = RenderPage("deadtoken");

        var empty = page.FindComponent<EmptyState>();
        Assert.Equal("This invite link no longer works.", empty.Instance.Message);

        page.Find(".pspad-empty-state").Click();

        var navigation = page.Services.GetRequiredService<NavigationManager>();
        Assert.Equal(navigation.BaseUri, navigation.Uri);
    }

    [Fact]
    public void OfflineOffersARetryThatJoinsOnceBackOnline()
    {
        var listId = Guid.NewGuid();
        var connectivity = new ToggleableConnectivity(isOnline: false);
        Arrange(Joined(listId), connectivity: connectivity);

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

        var page = RenderPage("abc123");

        var empty = page.FindComponent<EmptyState>();
        Assert.Equal("Joining needs a connection.", empty.Instance.Message);
    }

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

        public (string Token, string Code)? JoinedWith { get; private set; }

        public Task<JoinOutcome> JoinAsync(string token, string code)
        {
            JoinedWith = (token, code);
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
