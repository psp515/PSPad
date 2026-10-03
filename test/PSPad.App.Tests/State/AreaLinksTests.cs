using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class AreaLinksTests
{
    [Fact]
    public void AnOwnedAreaLinksToItsOwnRoute()
    {
        var areaId = Guid.NewGuid();

        Assert.Equal($"/areas/{areaId}", AreaLinks.For(areaId));
    }

    [Fact]
    public void SharedWithMeLinksToItsReadableRoute()
    {
        Assert.Equal("/areas/shared", AreaLinks.For(SharedWithMe.AreaId));
    }
}
