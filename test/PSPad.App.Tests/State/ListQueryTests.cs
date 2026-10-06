using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ListQueryTests
{
    [Fact]
    public void AnExistingListUrlKeepsTheScreenAndOpensOnlyThatList()
    {
        var listId = Guid.NewGuid();

        var uri = ListQuery.For($"https://pspad.local/lists/{listId}?task={Guid.NewGuid()}", listId);

        Assert.Equal($"https://pspad.local/lists/{listId}?list={listId}", uri);
        Assert.Equal(listId, ListQuery.From(uri));
        Assert.Null(ListQuery.NewListAreaFrom(uri));
    }

    [Fact]
    public void ANewTaskOrItemUrlNeverOpensTheListPanel()
    {
        var listId = Guid.NewGuid();

        Assert.Null(ListQuery.From(TaskQuery.ForNewTask("https://pspad.local/", listId)));
        Assert.Null(ListQuery.From(ReferenceQuery.ForNewItem("https://pspad.local/", listId)));
    }

    [Fact]
    public void ANewListUrlIsNotAnExistingList()
    {
        var areaId = Guid.NewGuid();

        Assert.Null(ListQuery.From(ListQuery.ForNewList($"https://pspad.local/areas/{areaId}", areaId)));
    }

    [Fact]
    public void AUrlWithNoQueryAddsNoList()
    {
        Assert.Null(ListQuery.NewListAreaFrom("https://pspad.local/areas/" + Guid.NewGuid()));
    }

    [Fact]
    public void ANewListUrlCarriesItsAreaAndKeepsTheScreen()
    {
        var areaId = Guid.NewGuid();

        var uri = ListQuery.ForNewList($"https://pspad.local/areas/{areaId}?task={Guid.NewGuid()}", areaId);

        Assert.Equal($"https://pspad.local/areas/{areaId}?list=new&inarea={areaId}", uri);
        Assert.Equal(areaId, ListQuery.NewListAreaFrom(uri));
    }

    [Fact]
    public void ANewListUrlOpensNeitherTheAreaPanelNorATask()
    {
        var areaId = Guid.NewGuid();

        var uri = ListQuery.ForNewList($"https://pspad.local/areas/{areaId}", areaId);

        Assert.Null(AreaQuery.From(uri));
        Assert.False(AreaQuery.IsNew(uri));
        Assert.Null(TaskQuery.NewTaskListFrom(uri));
    }

    [Fact]
    public void ANewTaskUrlIsNotANewListUrl()
    {
        var listId = Guid.NewGuid();

        Assert.Null(ListQuery.NewListAreaFrom(TaskQuery.ForNewTask("https://pspad.local/", listId)));
    }

    static readonly Guid ViewId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Theory]
    [InlineData("https://x/areas/a?list=44444444-4444-4444-4444-444444444444", ListPanelView.Details)]
    [InlineData("https://x/areas/a?list=44444444-4444-4444-4444-444444444444&view=members", ListPanelView.Members)]
    [InlineData("https://x/areas/a?list=44444444-4444-4444-4444-444444444444&view=snapshots", ListPanelView.Snapshots)]
    [InlineData("https://x/areas/a?list=44444444-4444-4444-4444-444444444444&view=bogus", ListPanelView.Details)]
    public void ViewIsReadFromTheQuery(string uri, ListPanelView expected) =>
        Assert.Equal(expected, ListQuery.ViewFrom(uri));

    [Fact]
    public void ForAViewAddsIt() =>
        Assert.Equal($"https://x/areas/a?list={ViewId}&view=members",
            ListQuery.For("https://x/areas/a?list=old", ViewId, ListPanelView.Members));

    [Fact]
    public void ForDetailsLeavesItOut() =>
        Assert.Equal($"https://x/areas/a?list={ViewId}", ListQuery.For("https://x/areas/a", ViewId, ListPanelView.Details));
}
