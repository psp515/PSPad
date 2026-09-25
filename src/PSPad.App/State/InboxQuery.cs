namespace PSPad.App.State;

public static class InboxQuery
{
    const string NewItem = "new";

    public static Guid? From(string uri) =>
        Guid.TryParse(QueryString.Value(uri, "inbox"), out var id) ? id : null;

    public static bool IsNew(string uri) => QueryString.Value(uri, "inbox") == NewItem;

    public static string ForNew(string uri) => $"{QueryString.Without(uri)}?inbox={NewItem}";

    public static string For(string uri, Guid itemId) => $"{QueryString.Without(uri)}?inbox={itemId}";
}
