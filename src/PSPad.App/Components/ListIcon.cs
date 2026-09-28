using MudBlazor;
using PSPad.Module.Tasks.Lists;

namespace PSPad.App.Components;

public static class ListIcon
{
    public static string For(TaskList list) => For(list.Kind);

    public static string For(ListKind kind) => kind switch
    {
        ListKind.Reference => Icons.Material.Outlined.LibraryBooks,
        _ => Icons.Material.Outlined.Checklist
    };
}
