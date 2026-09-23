using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Layout;
using PSPad.App.Pages;
using PSPad.TestInfrastructure;
using System.Reflection;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class WelcomeTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 3, 10);

    public WelcomeTests() => AppTestHost.Arrange(this, Guid.NewGuid(), Today);

    [Fact]
    public void ItNeverMountsAppShell()
    {
        // AppShell loads a session, starts sync and draws the sidebar. This page exists precisely
        // for the visitor who has none of that.
        var layout = typeof(Welcome).GetCustomAttribute<LayoutAttribute>();

        Assert.NotNull(layout);
        Assert.Equal(typeof(PublicLayout), layout!.LayoutType);
    }

    [Fact]
    public void ItLeadsWithTheBrandAndTheHeadline()
    {
        var page = Render<Welcome>();

        Assert.Contains("pspad-boot-mark", page.Markup);
        Assert.Contains("Your day, planned.", page.Markup);
    }

    [Fact]
    public void TheTaglineLinksToWhatGtdIs()
    {
        var page = Render<Welcome>();

        var tagline = page.Find(".pspad-welcome-tagline");
        Assert.Contains("An approach for GTD techniques", tagline.TextContent);
        Assert.Equal("https://en.wikipedia.org/wiki/Getting_Things_Done", tagline.GetAttribute("href"));
    }

    [Fact]
    public void ItsPrimaryActionIsSigningIn()
    {
        var page = Render<Welcome>();

        var login = page.Find(".pspad-welcome-login");
        Assert.Equal("authentication/login", login.GetAttribute("href"));
        Assert.Contains("Log in", login.TextContent);
    }

    [Fact]
    public void ItCarriesTheReturnAddressThroughToTheLogin()
    {
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("welcome?returnUrl=%2Finbox");

        var page = Render<Welcome>();

        Assert.Equal(
            "authentication/login?returnUrl=%2Finbox",
            page.Find(".pspad-welcome-login").GetAttribute("href"));
    }

    [Fact]
    public void ItSendsAnyoneWantingDetailToTheDocumentationSite()
    {
        var page = Render<Welcome>();

        Assert.Equal("https://psp515.com/pspad", page.Find(".pspad-welcome-docs").GetAttribute("href"));
    }

    [Fact]
    public void ItPreviewsMyDayWithRealTaskRows()
    {
        var page = Render<Welcome>();

        Assert.Contains("Renew the domain", page.Markup);
        Assert.Contains("Draft the release notes", page.Markup);
        Assert.Contains("Read a book", page.Markup);
        Assert.Contains("Water the plants", page.Markup);
        Assert.Equal(4, page.FindComponents<PSPad.App.Components.TaskRow>().Count);
    }

    [Fact]
    public void ItNoLongerListsTheFeatureCards()
    {
        // The hero and the My Day preview are the whole screen now -- feature detail lives on the docs site.
        var page = Render<Welcome>();

        Assert.DoesNotContain("One Today screen", page.Markup);
        Assert.DoesNotContain("One Inbox", page.Markup);
        Assert.DoesNotContain("Areas and lists", page.Markup);
    }

    [Theory]
    [InlineData("How it goes")]
    [InlineData("Run it yourself")]
    [InlineData("Install")]
    public void ItLeavesSelfHostingToTheDocumentationSite(string text)
    {
        // Whoever reaches this screen already has an instance -- they are standing in it.
        var page = Render<Welcome>();

        Assert.DoesNotContain(text, page.Markup);
    }
}
