using Bunit;
using Microsoft.AspNetCore.Components;
using PSPad.App.Layout;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class PublicLayoutTests : Bunit.TestContext
{
    public PublicLayoutTests() => AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 3, 10));

    [Fact]
    public void ThePublicScreensGetTheAppTheme()
    {
        var layout = Render<PublicLayout>(parameters => parameters.Add(page => page.Body, Content));

        Assert.Contains("--mud-palette-primary", layout.Markup);
        Assert.Contains("--mud-typography-default-family", layout.Markup);
    }

    static readonly RenderFragment Content = builder => builder.AddContent(0, "page");
}
