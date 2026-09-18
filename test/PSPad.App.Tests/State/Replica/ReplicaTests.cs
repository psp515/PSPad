using PSPad.App.State.Replica;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Replica;

[UnitTest]
public class ReplicaTests
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public async Task AnAggregateComesBackOutOfTheReplica()
    {
        var replica = new InMemoryReplica();
        var area = NewArea("Home");

        await replica.SaveAsync(area);

        var loaded = await replica.LoadAsync<Area>(area.Id);
        Assert.Equal("Home", loaded!.Name);
    }

    [Fact]
    public async Task LoadAllFiltersByUser()
    {
        var replica = new InMemoryReplica();
        await replica.SaveAsync(NewArea("Mine"));
        await replica.SaveAsync(NewArea("Theirs", Guid.NewGuid()));

        var mine = await replica.LoadAllAsync<Area>(User);

        Assert.Equal(["Mine"], mine.Select(area => area.Name));
    }

    [Fact]
    public async Task TheMarkerStartsAtZeroAndRemembersWhatItIsSetTo()
    {
        var replica = new InMemoryReplica();

        Assert.Equal(0, await replica.MarkerAsync());

        await replica.SetMarkerAsync(42);

        Assert.Equal(42, await replica.MarkerAsync());
    }

    [Fact]
    public async Task TheDocumentStoreReadsThroughTheReplica()
    {
        var replica = new InMemoryReplica();
        var area = NewArea("Home");
        await replica.SaveAsync(area);

        var store = new ReplicaDocumentStore<Area>(replica);

        Assert.Equal("Home", (await store.LoadAsync(area.Id, CancellationToken.None))!.Name);
    }

    static Area NewArea(string name, Guid? user = null)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), user ?? User, Guid.NewGuid(), name, 0),
            DateTimeOffset.UnixEpoch));
        return area;
    }
}
