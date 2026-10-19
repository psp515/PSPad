using Microsoft.Extensions.Localization;
using MudBlazor;
using PSPad.App.Resources;
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

    public static string LabelFor(TaskList list, IStringLocalizer<ListsStrings> localizer) =>
        LabelFor(list.Kind, localizer);

    public static string LabelFor(ListKind kind, IStringLocalizer<ListsStrings> localizer) => kind switch
    {
        ListKind.Reference => localizer["ReferenceListLabel"],
        _ => localizer["TaskListLabel"]
    };
}
