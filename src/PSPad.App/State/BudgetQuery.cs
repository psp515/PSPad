namespace PSPad.App.State;

public static class BudgetQuery
{
    const string NewBudget = "new";

    public static Guid? From(string uri) =>
        Guid.TryParse(QueryString.Value(uri, "budget"), out var id) ? id : null;

    public static bool IsNew(string uri) => QueryString.Value(uri, "budget") == NewBudget;

    public static string ForNew(string uri) => $"{QueryString.Without(uri)}?budget={NewBudget}";

    public static string For(string uri, Guid budgetId) => $"{QueryString.Without(uri)}?budget={budgetId}";
}
