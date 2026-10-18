using Microsoft.Extensions.Localization;
using PSPad.App.Localization;
using PSPad.App.Resources;

namespace PSPad.App.Sync;

public static class RelativeTime
{
    public static string Describe(DateTimeOffset then, DateTimeOffset now, IStringLocalizer<ShellStrings> text)
    {
        var elapsed = now - then;

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return text["TimeJustNow"];
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return text["TimeMinutesAgo", (int)elapsed.TotalMinutes];
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return text.Plural((int)elapsed.TotalHours, "TimeHoursAgo");
        }

        return text.Plural((int)elapsed.TotalDays, "TimeDaysAgo");
    }
}
