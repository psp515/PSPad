using PSPad.App.Api;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Api;

[UnitTest]
public class SnapshotLinksTests
{
    [Fact]
    public void ItBuildsAPublicSnapshotLinkUnderPublicSnapshot()
    {
        var link = SnapshotLinks.For("https://pspad.example", "tok-1");

        Assert.Equal("https://pspad.example/public/snapshot/tok-1", link);
    }

    [Fact]
    public void TheRouteIsRelativeAndSansOrigin()
    {
        Assert.Equal("/public/snapshot/tok-1", SnapshotLinks.Route("tok-1"));
    }
}
