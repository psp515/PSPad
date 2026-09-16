using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using PSPad.App.Layout;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class AccountMenuTests : Bunit.TestContext
{
    static readonly Guid User = Guid.Parse("6f1d2c3b-0000-4000-8000-000000000001");

    [Theory]
    [InlineData("History")]
    [InlineData("Settings")]
    [InlineData("Sign out")]
    public void ItOffersEveryEntry(string entry)
    {
        Arrange();

        var menu = Render(BuildMenu("Ada Lovelace", "ada@example.com", User));
        OpenMenu(menu);

        Assert.Contains(entry, menu.Markup);
    }

    [Fact]
    public void ItNoLongerOffersGoalsNowThatTheSidebarDoes()
    {
        Arrange();

        var menu = Render(BuildMenu("Ada Lovelace", "ada@example.com", User));
        OpenMenu(menu);

        Assert.DoesNotContain("Goals", menu.Markup);
    }

    [Fact]
    public void ItShowsTheNameAndEmailOnItsActivator()
    {
        Arrange();

        var menu = Render<AccountMenu>(parameters => parameters
            .Add(account => account.DisplayName, "Ada Lovelace")
            .Add(account => account.Email, "ada@example.com")
            .Add(account => account.UserId, User));

        Assert.Contains("Ada Lovelace", menu.Markup);
        Assert.Contains("ada@example.com", menu.Markup);
    }

    [Fact]
    public void ItOffersSettingsHistoryAndSignOutAndNoThemeToggle()
    {
        Arrange();

        var menu = Render(BuildMenu("Ada Lovelace", "ada@example.com", User));
        OpenMenu(menu);

        Assert.Contains("/settings", menu.Markup);
        Assert.Contains("/history", menu.Markup);
        Assert.Contains("/authentication/logout", menu.Markup);
        Assert.DoesNotContain("Theme", menu.Markup);
    }

    [Fact]
    public void TheAvatarFallsBackToTheEmailWhenThereIsNoName()
    {
        Arrange();

        var menu = Render<AccountMenu>(parameters => parameters
            .Add(account => account.DisplayName, "")
            .Add(account => account.Email, "ada@example.com")
            .Add(account => account.UserId, User));

        Assert.Contains(">A<", menu.Markup);
    }

    [Fact]
    public void TheAvatarOnlyActivatorHasPadding()
    {
        Arrange();

        var menu = Render<AccountMenu>(parameters => parameters
            .Add(account => account.DisplayName, "Ada Lovelace")
            .Add(account => account.Email, "ada@example.com")
            .Add(account => account.UserId, User)
            .Add(account => account.AvatarOnly, true));

        var wrapper = menu.Find(".pspad-account-avatar-only");
        Assert.Contains("px-3", wrapper.ClassList);
        Assert.Contains("py-1", wrapper.ClassList);
    }

    void Arrange() => AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));

    // MudMenu renders ChildContent into MudPopoverProvider's portal, not inline, so both
    // must share one render tree for the popover content to reach the rendered markup.
    static RenderFragment BuildMenu(string displayName, string email, Guid userId) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<AccountMenu>(1);
        builder.AddAttribute(2, nameof(AccountMenu.Email), email);
        builder.AddAttribute(3, nameof(AccountMenu.DisplayName), displayName);
        builder.AddAttribute(4, nameof(AccountMenu.UserId), userId);
        builder.CloseComponent();
    };

    // The activator has no click handler in this MudBlazor version, only a keydown one;
    // Enter drives the same open path a pointer click reaches through JS interop.
    static void OpenMenu<TComponent>(IRenderedComponent<TComponent> host) where TComponent : IComponent =>
        host.Find(".mud-menu-activator").KeyDown(new KeyboardEventArgs { Key = "Enter" });
}
