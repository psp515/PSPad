namespace PSPad.App.State;

public static class BudgetLinks
{
    public const string Index = "/budgets";

    public const string EntriesImport = "entries";

    public const string BalancesImport = "balances";

    public static string For(Guid budgetId) => $"/budgets/{budgetId}";

    public static string ForImport(Guid budgetId, string kind) => $"/budgets/{budgetId}/import/{kind}";

    public static string ForTab(Guid budgetId, string tab) => $"/budgets/{budgetId}/{tab}";
}
