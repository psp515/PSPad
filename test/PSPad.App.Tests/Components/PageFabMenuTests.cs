using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class PageFabMenuTests : Bunit.TestContext
{
    public PageFabMenuTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Fact]
    public void TheActivatorSitsInTheFixedBottomRightSpot()
    {
        var page = Render(WithOnePlainItem());

        page.Find(".pspad-fab");
    }

    [Fact]
    public void TheActivatorCarriesAnAccessibleName()
    {
        var page = Render(WithOnePlainItem());

        var activator = page.Find(".pspad-fab .mud-menu-activator button");

        Assert.Equal("Actions", activator.GetAttribute("aria-label"));
    }

    [Fact]
    public void ClickingTheActivatorOpensTheMenuAndShowsItsItem()
    {
        var page = Render(WithOnePlainItem());

        page.Find(".pspad-fab .mud-menu-activator").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        page.Find(".mud-menu-item");
    }

    [Fact]
    public void ClickingAnItemInvokesItsOwnHandler()
    {
        var clicked = false;
        var page = Render(WithOneItem(() => clicked = true));

        page.Find(".pspad-fab .mud-menu-activator").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        page.Find(".mud-menu-item").Click();

        Assert.True(clicked);
    }

    [Fact]
    public void AnIconOnlyItemCarriesNoVisibleText()
    {
        var page = Render(WithOnePlainItem());

        page.Find(".pspad-fab .mud-menu-activator").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        var item = page.Find(".mud-menu-item");

        Assert.True(string.IsNullOrWhiteSpace(item.TextContent));
        Assert.NotNull(item.QuerySelector("svg"));
    }

    RenderFragment WithOnePlainItem() => WithOneItem(() => { });

    RenderFragment WithOneItem(Action onClick) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<PageFabMenu>(1);
        builder.AddAttribute(2, nameof(PageFabMenu.ChildContent), (RenderFragment)(itemBuilder =>
        {
            itemBuilder.OpenComponent<MudMenuItem>(0);
            itemBuilder.AddAttribute(1, nameof(MudMenuItem.Icon), Icons.Material.Filled.Add);
            itemBuilder.AddAttribute(2, nameof(MudMenuItem.OnClick),
                EventCallback.Factory.Create<MouseEventArgs>(this, onClick));
            itemBuilder.CloseComponent();
        }));
        builder.CloseComponent();
    };
}
