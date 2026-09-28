using MudBlazor;
using PSPad.App.Components;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ListIconTests
{
    [Fact]
    public void ATasksListShowsTheChecklistIcon()
    {
        Assert.Equal(Icons.Material.Outlined.Checklist, ListIcon.For(ListKind.Tasks));
    }

    [Fact]
    public void AReferenceListShowsTheLibraryBooksIcon()
    {
        Assert.Equal(Icons.Material.Outlined.LibraryBooks, ListIcon.For(ListKind.Reference));
    }

    [Fact]
    public void ItReadsTheKindOffTheList()
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null,
            new CreateTaskList(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Recipes", 0,
                ListKind.Reference),
            DateTimeOffset.UnixEpoch));

        Assert.Equal(Icons.Material.Outlined.LibraryBooks, ListIcon.For(list));
    }
}
