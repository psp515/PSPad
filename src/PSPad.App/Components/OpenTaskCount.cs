using Microsoft.Extensions.Localization;
using PSPad.App.Localization;
using PSPad.App.Resources;

namespace PSPad.App.Components;

public static class OpenTaskCount
{
    public static string Describe(int open, IStringLocalizer<TasksStrings> localizer) =>
        localizer.Plural(open, "OpenTasks");
}
