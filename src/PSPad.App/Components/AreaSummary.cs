using Microsoft.Extensions.Localization;
using PSPad.App.Localization;
using PSPad.App.Resources;

namespace PSPad.App.Components;

public static class AreaSummary
{
    public static string Describe(
        int taskLists, int referenceLists, int openTasks, int items,
        IStringLocalizer<ListsStrings> localizer, IStringLocalizer<TasksStrings> tasks)
    {
        if (referenceLists == 0)
        {
            return $"{localizer.Plural(taskLists, "SummaryLists")} · {OpenTaskCount.Describe(openTasks, tasks)}";
        }

        if (taskLists == 0)
        {
            return $"{localizer.Plural(referenceLists, "SummaryReferenceLists")} · {localizer.Plural(items, "SummaryItems")}";
        }

        var parts = new List<string> { localizer.Plural(taskLists + referenceLists, "SummaryLists") };

        if (openTasks > 0 || items == 0)
        {
            parts.Add(OpenTaskCount.Describe(openTasks, tasks));
        }

        if (items > 0)
        {
            parts.Add(localizer.Plural(items, "SummaryItems"));
        }

        return string.Join(" · ", parts);
    }
}
