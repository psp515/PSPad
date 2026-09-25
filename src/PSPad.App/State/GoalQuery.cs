namespace PSPad.App.State;

public static class GoalQuery
{
    const string NewGoal = "new";

    public static Guid? From(string uri) =>
        Guid.TryParse(QueryString.Value(uri, "goal"), out var id) ? id : null;

    public static bool IsNew(string uri) => QueryString.Value(uri, "goal") == NewGoal;

    public static string ForNew(string uri) => $"{QueryString.Without(uri)}?goal={NewGoal}";

    public static string For(string uri, Guid goalId) => $"{QueryString.Without(uri)}?goal={goalId}";
}
