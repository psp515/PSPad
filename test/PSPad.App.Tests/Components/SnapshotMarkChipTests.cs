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
}
