using Bunit;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class RowGroupTests : Bunit.TestContext
{
    [Fact]
    public void ItIsOneOutlinedPaperHoldingItsRows()
    {
        var group = Render<RowGroup>(parameters => parameters
            .AddChildContent("<div class=\"pspad-row\">A</div><div class=\"pspad-row\">B</div>"));

        var paper = group.Find(".mud-paper");
        Assert.Contains("mud-paper-outlined", paper.ClassList);
        Assert.Contains("pspad-row-group", paper.ClassList);
        Assert.Equal(2, paper.QuerySelectorAll(".pspad-row").Length);
    }

    [Fact]
    public void ColumnsMarkTheGroupSoRowsFlowIntoColumnsOnWideScreens()
    {
        var group = Render<RowGroup>(parameters => parameters.Add(p => p.Columns, true));

        Assert.Contains("pspad-row-group-cols", group.Find(".pspad-row-group").ClassList);
        Assert.DoesNotContain("pspad-row-group-cols", Render<RowGroup>().Find(".pspad-row-group").ClassList);
    }

    [Fact]
    public void ItTakesAnExtraClass()
    {
        var group = Render<RowGroup>(parameters => parameters.Add(p => p.Class, "pspad-schedule"));

        Assert.Contains("pspad-schedule", group.Find(".pspad-row-group").ClassList);
    }
}
