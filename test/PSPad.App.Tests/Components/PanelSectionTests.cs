using Bunit;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class PanelSectionTests : Bunit.TestContext
{
    [Fact]
    public void ItShowsItsTitleAboveItsContentAfterADivider()
    {
        Services.AddMudServices();

        var section = Render<PanelSection>(parameters => parameters
            .Add(p => p.Title, "Description")
            .AddChildContent("<p class=\"inside\">Body</p>"));

        Assert.Equal("Description", section.Find(".pspad-panel-section-title").TextContent.Trim());
        Assert.NotNull(section.Find(".pspad-panel-section .inside"));
        var markup = section.Markup;
        Assert.True(markup.IndexOf("mud-divider", StringComparison.Ordinal)
            < markup.IndexOf("pspad-panel-section-title", StringComparison.Ordinal));
    }
}
