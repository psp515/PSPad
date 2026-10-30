using Microsoft.Extensions.Localization;
using PSPad.App.Resources;

namespace PSPad.App.State;

public static class DueDayText
{
    public static string Describe(DateOnly day, DateOnly today, IStringLocalizer<SharedStrings> shared) => day switch
    {
        var d when d == today => shared["Today"],
        var d when d == today.AddDays(1) => shared["Tomorrow"],
        var d when d == today.AddDays(-1) => shared["Yesterday"],
        var d when d.Year == today.Year => d.ToString("ddd, d MMM"),
        var d => d.ToString("ddd, d MMM yyyy")
    };
}
