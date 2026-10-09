using Bunit;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class SnapshotMarkChipTests : Bunit.TestContext
{
    [Fact]
    public void ItShowsTheMarkedLabel()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();

        var chip = Render<SnapshotMarkChip>();

        Assert.Contains("Marked on a snapshot", chip.Markup);
        Assert.Contains("pspad-snapshot-mark", chip.Markup);
    }

    [Fact]
    public void TheCompactChipIsAnIconNamedForScreenReaders()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();

        var chip = Render<SnapshotMarkChip>(parameters => parameters.Add(c => c.Compact, true));

        var root = chip.Find(".pspad-snapshot-mark");
        Assert.Contains("pspad-snapshot-mark-compact", root.ClassName);
        Assert.Equal("Marked on a snapshot", root.GetAttribute("aria-label"));
        Assert.Equal("Marked on a snapshot", root.GetAttribute("title"));
        Assert.DoesNotContain("Marked on a snapshot", root.TextContent);
    }
}
