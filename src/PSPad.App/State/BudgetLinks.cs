namespace PSPad.App.State;

public static class BudgetLinks
{
    public const string Index = "/budgets";

    public static string For(Guid budgetId) => $"/budgets/{budgetId}";

    public static string ForTab(Guid budgetId, string tab) => $"/budgets/{budgetId}/{tab}";
}
