namespace PSPad.App.State;

public static class BudgetLinks
{
    public const string Index = "/budgets";

    public static string For(Guid budgetId) => $"/budgets/{budgetId}";
}
