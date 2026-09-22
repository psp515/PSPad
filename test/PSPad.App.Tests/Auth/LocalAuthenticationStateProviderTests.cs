using System.Security.Claims;
using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class LocalAuthenticationStateProviderTests
{
    static readonly LocalSession Stored = new(
        Guid.NewGuid(), "Zoe", "zoe@example.com", "Europe/Warsaw", "refresh",
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task ItAuthenticatesFromTheSessionAloneWithNoAccessToken()
    {
        var provider = new LocalAuthenticationStateProvider(new InMemoryLocalSessionStore(Stored));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal(Stored.UserId.ToString(), state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Zoe", state.User.Identity?.Name);
    }

    [Fact]
    public async Task ItStaysAnonymousWithoutASession()
    {
        var provider = new LocalAuthenticationStateProvider(new InMemoryLocalSessionStore());

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task ItPublishesSignIn()
    {
        var provider = new LocalAuthenticationStateProvider(new InMemoryLocalSessionStore());
        var notified = 0;
        provider.AuthenticationStateChanged += _ => notified++;

        provider.SignedIn(Stored);

        Assert.Equal(1, notified);
        Assert.True((await provider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task ItPublishesSignOut()
    {
        var provider = new LocalAuthenticationStateProvider(new InMemoryLocalSessionStore(Stored));
        await provider.GetAuthenticationStateAsync();

        provider.SignedOut();

        Assert.False((await provider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated);
    }
}
