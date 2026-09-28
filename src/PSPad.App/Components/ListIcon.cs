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

    public static string LabelFor(TaskList list) => LabelFor(list.Kind);

    public static string LabelFor(ListKind kind) => kind switch
    {
        ListKind.Reference => "Reference list",
        _ => "Task list"
    };
}
