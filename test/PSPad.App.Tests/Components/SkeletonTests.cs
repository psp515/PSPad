using Bunit;
using MudBlazor;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class SkeletonTests : Bunit.TestContext
{
    [Fact]
    public void ARowSkeletonRendersTheRequestedNumberOfRows()
    {
        var skeleton = Render<RowSkeleton>(parameters => parameters.Add(row => row.Count, 4));

        Assert.Equal(4, skeleton.FindAll(".pspad-row").Count);
    }

    [Fact]
    public void ACardSkeletonRendersTheRequestedNumberOfCards()
    {
        var skeleton = Render<CardSkeleton>(parameters => parameters.Add(card => card.Count, 2));

        Assert.Equal(2, skeleton.FindAll(".mud-grid-item").Count);
    }

    [Fact]
    public void ARowSkeletonSitsInsideOneRowGroupAndACardSkeletonInsideTheGrid()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Assert.NotNull(Render<RowSkeleton>().Find(".pspad-row-group .pspad-row"));
        Assert.NotNull(Render<CardSkeleton>().Find(".mud-grid .mud-grid-item"));
    }

    [Fact]
    public void ARowSkeletonMarksItselfAsALiveLoadingRegion()
    {
        var root = Render<RowSkeleton>().Find("[role=status]");

        Assert.Equal("status", root.GetAttribute("role"));
        Assert.Equal("true", root.GetAttribute("aria-busy"));
    }

    [Fact]
    public void ACardSkeletonMarksItselfAsALiveLoadingRegion()
    {
        var root = Render<CardSkeleton>().Find(".mud-grid");

        Assert.Equal("status", root.GetAttribute("role"));
        Assert.Equal("true", root.GetAttribute("aria-busy"));
    }
}
