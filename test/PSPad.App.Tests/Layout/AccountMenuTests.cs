using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PSPad.App.Layout;
using PSPad.App.Theme;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class AccountMenuTests : Bunit.TestContext
{
    static readonly Guid User = Guid.Parse("6f1d2c3b-0000-4000-8000-000000000001");

    [Theory]
    [InlineData("Goals")]
    [InlineData("History")]
    [InlineData("Theme")]
    [InlineData("Sign out")]
    public void ItOffersEveryEntry(string entry)
    {
        Arrange();

        var menu = Render(BuildMenu("kolberu@gmail.com", User, pendingCommands: 0));
        OpenMenu(menu);

        Assert.Contains(entry, menu.Markup);
    }

    [Fact]
    public void ItShowsTheInitialOfTheAddress()
    {
        Arrange();

        var menu = Render(BuildMenu("kolberu@gmail.com", User, pendingCommands: 0));

        Assert.Contains(">K<", menu.Markup);
    }

    [Fact]
    public void ItReportsPendingCommandsWhenThereAreAny()
    {
        Arrange();

        var menu = Render(BuildMenu("kolberu@gmail.com", User, pendingCommands: 3));
        OpenMenu(menu);

        Assert.Contains("Sync: 3 pending", menu.Markup);
    }

    [Fact]
    public void ItDoesNotOfferSyncWhenNothingIsPending()
    {
        Arrange();

        var menu = Render(BuildMenu("kolberu@gmail.com", User, pendingCommands: 0));
        OpenMenu(menu);

        Assert.DoesNotContain("Sync:", menu.Markup);
    }

    void Arrange()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new ThemePreference(JSInterop.JSRuntime));
    }

    // MudMenu renders ChildContent into MudPopoverProvider's portal, not inline, so both
    // must share one render tree for the popover content to reach the rendered markup.
    static RenderFragment BuildMenu(string email, Guid userId, int pendingCommands) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<AccountMenu>(1);
        builder.AddAttribute(2, nameof(AccountMenu.Email), email);
        builder.AddAttribute(3, nameof(AccountMenu.UserId), userId);
        builder.AddAttribute(4, nameof(AccountMenu.PendingCommands), pendingCommands);
        builder.CloseComponent();
    };

    // The activator has no click handler in this MudBlazor version, only a keydown one;
    // Enter drives the same open path a pointer click reaches through JS interop.
    static void OpenMenu<TComponent>(IRenderedComponent<TComponent> host) where TComponent : IComponent =>
        host.Find(".mud-menu-activator").KeyDown(new KeyboardEventArgs { Key = "Enter" });
}
