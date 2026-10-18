using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Resources;

namespace PSPad.App.Components;

public static class AreaSummary
{
    public static string Describe(
        int taskLists, int referenceLists, int openTasks, int items, IStringLocalizer<TasksStrings> localizer)
    {
        if (referenceLists == 0)
        {
            return $"{Counted(taskLists, "list", "lists")} · {OpenTaskCount.Describe(openTasks, localizer)}";
        }

        if (taskLists == 0)
        {
            return $"{Counted(referenceLists, "reference list", "reference lists")} · {ItemCount(items)}";
        }

        var parts = new List<string> { Counted(taskLists + referenceLists, "list", "lists") };

        if (openTasks > 0 || items == 0)
        {
            parts.Add(OpenTaskCount.Describe(openTasks, localizer));
        }

        if (items > 0)
        {
            parts.Add(ItemCount(items));
        }

        return string.Join(" · ", parts);
    }

    static string ItemCount(int items) => items == 0 ? "no items" : Counted(items, "item", "items");

    static string Counted(int count, string one, string many) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? one : many)}";
}
