using Bunit;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class BrandLoaderTests : Bunit.TestContext
{
    [Fact]
    public void ItDrawsTheBrandCheck()
    {
        var loader = Render<BrandLoader>();

        Assert.Contains("M184,272 l48,48 l96,-120", loader.Markup);
    }
}
