using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ListQueryTests
{
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
}
