using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Layout;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class BottomNavTests : Bunit.TestContext
{
    [Fact]
    public void ItOffersTodayInboxAndAreas()
    {
        Arrange();

        var nav = Render<BottomNav>();

        Assert.Contains("Today", nav.Markup);
        Assert.Contains("Inbox", nav.Markup);
        Assert.Contains("Areas", nav.Markup);
    }

    [Fact]
    public async Task TappingAreasAsksForTheSheetRatherThanNavigating()
    {
        Arrange();
        var asked = false;

        var nav = Render<BottomNav>(parameters => parameters
            .Add(p => p.OnAreasRequested, () => asked = true));
        await nav.FindAll("button")[2].ClickAsync(new());

        Assert.True(asked);
    }

    void Arrange()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();
    }
}
