using System.Text.Json;
using PSPad.App.State;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class IndexedDbReplicaTests
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ADocumentSerialisesWithItsTypeAndOwner()
    {
        var area = NewArea("Home");

        var row = ReplicaRow.From(area);

        Assert.Equal(nameof(Area), row.Type);
        Assert.Equal(User, row.UserId);
        Assert.Equal(area.Id, row.Id);
    }

    [Fact]
    public void ARowRoundTripsBackIntoItsAggregate()
    {
        var area = NewArea("Home");

        var restored = ReplicaRow.From(area).To<Area>();

        Assert.Equal("Home", restored.Name);
        Assert.Equal(area.Version, restored.Version);
        Assert.Equal(area.Seq, restored.Seq);
    }

    [Fact]
    public void ARowFromTheServerRestoresTheSameWay()
    {
        var area = NewArea("Home");
        var fromServer = JsonSerializer.SerializeToElement(area);

        var restored = ReplicaRow.FromServer(nameof(Area), User, area.Id, fromServer).To<Area>();

        Assert.Equal("Home", restored.Name);
    }

    static Area NewArea(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), DateTimeOffset.UnixEpoch));
        return area;
    }
}
