namespace PSPad.App.State;

public static class BudgetMembersQuery
{
    public static Guid? From(string uri) =>
        Guid.TryParse(QueryString.Value(uri, "members"), out var id) ? id : null;

    public static string For(string uri, Guid budgetId) => $"{QueryString.Without(uri)}?members={budgetId}";
}
