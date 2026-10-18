using Bunit;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class AddRowTests : Bunit.TestContext
{
    [Fact]
    public void TheIconAndTheLabelSitOnOneLineUnderTheCheckboxColumn()
    {
        var row = Render<AddRow>(parameters => parameters.Add(p => p.Text, "Add task"));

        var root = row.Find(".pspad-row-add");
        Assert.Contains("d-flex", root.ClassList);
        Assert.Contains("align-center", root.ClassList);
        Assert.NotNull(root.QuerySelector(".pspad-priority-slot"));
        Assert.NotNull(root.QuerySelector(".pspad-row-add-icon"));
        Assert.Contains("Add task", root.TextContent);
    }

    [Fact]
    public void EnterActivatesIt()
    {
        var clicked = 0;
        var row = Render<AddRow>(parameters => parameters
            .Add(p => p.Text, "Add task")
            .Add(p => p.OnClick, () => clicked++));

        row.Find(".pspad-row-add").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(1, clicked);
    }
}
