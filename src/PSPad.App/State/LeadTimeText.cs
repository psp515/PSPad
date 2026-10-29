using Microsoft.Extensions.Localization;
using PSPad.App.Localization;
using PSPad.App.Resources;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public static class LeadTimeText
{
    static readonly LeadTime DefaultLookAhead = new(1, LeadUnit.Weeks);

    public static string Describe(LeadTime? lead, IStringLocalizer<TasksStrings> localizer)
    {
        var shown = lead ?? DefaultLookAhead;
        return localizer.Plural(shown.Amount, shown.Unit switch
        {
            LeadUnit.Weeks => "LeadWeeks",
            LeadUnit.Months => "LeadMonths",
            _ => "LeadDays"
        });
    }

    public static string UnitLabel(LeadUnit unit, int amount, IStringLocalizer<TasksStrings> localizer) =>
        localizer.Plural(amount, unit switch
        {
            LeadUnit.Weeks => "UnitWeeks",
            LeadUnit.Months => "UnitMonths",
            _ => "UnitDays"
        });
}
