using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Api;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.Tests;
using PSPad.Contracts;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class SettingsPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 3, 10);

    [Fact]
    public void ItShowsTheAccountName()
    {
        Arrange(displayName: "Ada Lovelace", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Contains("Ada Lovelace", page.Markup);
        Assert.Contains("ada@example.com", page.Markup);
    }

    [Fact]
    public async Task ChoosingATimeZoneSendsItAndRefreshesToday()
    {
        var state = Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        await page.InvokeAsync(() => page.Instance.ApplyTimeZoneAsync("Pacific/Kiritimati"));

        Assert.Equal("Pacific/Kiritimati", state.TimeZone);
        Assert.Equal(
            TodayRule.TodayIn(DateTimeOffset.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Pacific/Kiritimati")),
            state.Today);
    }

    [Fact]
    public void ItOffersTheThreeThemeModes()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Contains("System", page.Markup);
        Assert.Contains("Light", page.Markup);
        Assert.Contains("Dark", page.Markup);
    }

    AppState Arrange(string displayName, string email)
    {
        AppTestHost.Arrange(this, User, Today);

        var state = new AppState
        {
            UserId = User,
            Today = Today,
            DisplayName = displayName,
            Email = email
        };
        Services.AddSingleton(state);

        Services.AddSingleton(new PSPadApiClient(new HttpClient(new EchoTimeZoneHandler(User, displayName, email))
        {
            BaseAddress = new Uri("http://localhost/")
        }));

        return state;
    }

    sealed class EchoTimeZoneHandler(Guid userId, string displayName, string email) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadFromJsonAsync<SetTimeZoneRequest>(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new MeResponse(userId, displayName, email, body!.TimeZone))
            };
        }
    }
}
