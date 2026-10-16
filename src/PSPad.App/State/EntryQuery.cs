using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;

namespace PSPad.App.State;

public static class EntryQuery
{
    const string Expense = "expense";
    const string Income = "income";
    const string Month = "month";

    public static Guid? From(string uri) =>
        Guid.TryParse(QueryString.Value(uri, "entry"), out var id) ? id : null;

    public static (Guid BudgetId, CategoryKind Kind, YearMonth? Month)? NewFrom(string uri) =>
        Guid.TryParse(QueryString.Value(uri, Expense), out var expense) ? (expense, CategoryKind.Expense, MonthOf(uri))
        : Guid.TryParse(QueryString.Value(uri, Income), out var income) ? (income, CategoryKind.Income, MonthOf(uri))
        : null;

    public static string For(string uri, Guid entryId) => $"{QueryString.Without(uri)}?entry={entryId}";

    public static string ForNew(string uri, Guid budgetId, CategoryKind kind, YearMonth? month = null) =>
        $"{QueryString.Without(uri)}?{(kind == CategoryKind.Expense ? Expense : Income)}={budgetId}"
        + (month is { } chosen ? $"&{Month}={chosen}" : "");

    static YearMonth? MonthOf(string uri)
    {
        var text = QueryString.Value(uri, Month);

        if (text is null)
        {
            return null;
        }

        try
        {
            return YearMonth.Parse(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
