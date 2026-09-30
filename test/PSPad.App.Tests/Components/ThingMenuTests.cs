using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ThingMenuTests : Bunit.TestContext
{
    [Fact]
    public void ItOffersEditAndDeleteAndRaisesThem()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var edited = false;
        var deleted = false;

        var menu = Render(BuildMenu(() => edited = true, () => deleted = true));
        menu.Find("button").Click();

        var items = menu.FindAll(".mud-menu-item");
        Assert.Equal(["Edit", "Delete"], items.Select(item => item.TextContent.Trim()));

        items[0].Click();
        Assert.True(edited);

        menu.Find("button").Click();
        menu.FindAll(".mud-menu-item")[1].Click();
        Assert.True(deleted);
    }

    // MudMenu renders ChildContent into MudPopoverProvider's portal, not inline, so both
    // must share one render tree for the popover content to reach the rendered markup.
    static RenderFragment BuildMenu(Action onEdit, Action onDelete) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<ThingMenu>(1);
        builder.AddAttribute(2, nameof(ThingMenu.OnEdit), new EventCallback(null, onEdit));
        builder.AddAttribute(3, nameof(ThingMenu.OnDelete), new EventCallback(null, onDelete));
        builder.CloseComponent();
    };
}
