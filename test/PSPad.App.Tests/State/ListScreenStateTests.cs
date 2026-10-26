using PSPad.App.State;
using PSPad.App.Tests.Localization;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ListScreenStateTests
{
    readonly FakeStorageJsRuntime _js = new();
    readonly Guid _list = Guid.NewGuid();

    ListScreenState State => new(_js);

    [Fact]
    public async Task AFreshListLoadsTheDefaults() =>
        Assert.True((await State.LoadAsync(_list)).IsDefault);

    [Fact]
    public async Task AChoiceRoundTrips()
    {
        var goal = Guid.NewGuid();
        var choice = new ListScreenChoice(
            ListFilter.None with
            {
                When = new HashSet<WhenBucket> { WhenBucket.Overdue, WhenBucket.Today },
                Priorities = new HashSet<Priority> { Priority.High },
                Starred = true,
                GoalIds = new HashSet<Guid> { goal }
            },
            new ListSort(ListOrder.Name, true),
            ListScreenView.Calendar);

        await State.SaveAsync(_list, choice);
        var loaded = await State.LoadAsync(_list);

        Assert.Equal([WhenBucket.Overdue, WhenBucket.Today], loaded.Filter.When.Order());
        Assert.Equal([Priority.High], loaded.Filter.Priorities);
        Assert.True(loaded.Filter.Starred);
        Assert.Equal([goal], loaded.Filter.GoalIds);
        Assert.Equal(new ListSort(ListOrder.Name, true), loaded.Sort);
        Assert.Equal(ListScreenView.Calendar, loaded.View);
    }

    [Fact]
    public async Task EachListKeepsItsOwnChoice()
    {
        await State.SaveAsync(_list, ListScreenChoice.Default with { View = ListScreenView.Calendar });

        Assert.Equal(ListScreenView.List, (await State.LoadAsync(Guid.NewGuid())).View);
    }

    [Fact]
    public async Task SavingTheDefaultsRemovesTheKey()
    {
        await State.SaveAsync(_list, ListScreenChoice.Default with { View = ListScreenView.Calendar });
        await State.SaveAsync(_list, ListScreenChoice.Default);

        Assert.DoesNotContain(ListScreenState.KeyPrefix + _list, _js.Storage.Keys);
    }

    [Fact]
    public async Task MalformedJsonFallsBackToTheDefaults()
    {
        _js.Storage[ListScreenState.KeyPrefix + _list] = "{not json";

        Assert.True((await State.LoadAsync(_list)).IsDefault);
    }

    [Fact]
    public async Task UnknownNamesAreDroppedOneByOne()
    {
        _js.Storage[ListScreenState.KeyPrefix + _list] =
            """{"when":["Today","Someday","3"],"priority":["Urgent"],"order":"Colour","descending":false,"view":"Board"}""";

        var loaded = await State.LoadAsync(_list);

        Assert.Equal([WhenBucket.Today], loaded.Filter.When);
        Assert.Empty(loaded.Filter.Priorities);
        Assert.Equal(ListSort.Default, loaded.Sort);
        Assert.Equal(ListScreenView.List, loaded.View);
    }

    [Fact]
    public async Task AFailedWriteIsSwallowed()
    {
        _js.StorageWritesFail = true;

        await State.SaveAsync(_list, ListScreenChoice.Default with { View = ListScreenView.Calendar });
    }
}
