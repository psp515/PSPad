using System.Globalization;

namespace PSPad.App.Components;

public static class AreaSummary
{
    public static string Describe(int taskLists, int referenceLists, int openTasks, int items)
    {
        if (referenceLists == 0)
        {
            return $"{Counted(taskLists, "list", "lists")} · {OpenTaskCount.Describe(openTasks)}";
        }

        if (taskLists == 0)
        {
            return $"{Counted(referenceLists, "reference list", "reference lists")} · {ItemCount(items)}";
        }

        var parts = new List<string> { Counted(taskLists + referenceLists, "list", "lists") };

        if (openTasks > 0 || items == 0)
        {
            parts.Add(OpenTaskCount.Describe(openTasks));
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
