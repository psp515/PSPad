using Bunit;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class AddRowTests : Bunit.TestContext
{
    [Fact]
    public void EnterActivatesIt()
    {
        var clicked = 0;
        var row = Render<AddRow>(parameters => parameters
            .Add(p => p.Text, "Add task")
            .Add(p => p.OnClick, () => clicked++));

        row.Find("[role=button]").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(1, clicked);
    }
}
