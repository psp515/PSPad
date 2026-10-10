using PSPad.Module.Money.Budgets;

namespace PSPad.App.State;

public static class EntryQuery
{
    const string Expense = "expense";
    const string Income = "income";

    public static Guid? From(string uri) =>
        Guid.TryParse(QueryString.Value(uri, "entry"), out var id) ? id : null;

    public static (Guid BudgetId, CategoryKind Kind)? NewFrom(string uri) =>
        Guid.TryParse(QueryString.Value(uri, Expense), out var expense) ? (expense, CategoryKind.Expense)
        : Guid.TryParse(QueryString.Value(uri, Income), out var income) ? (income, CategoryKind.Income)
        : null;

    public static string For(string uri, Guid entryId) => $"{QueryString.Without(uri)}?entry={entryId}";

    public static string ForNew(string uri, Guid budgetId, CategoryKind kind) =>
        $"{QueryString.Without(uri)}?{(kind == CategoryKind.Expense ? Expense : Income)}={budgetId}";
}
