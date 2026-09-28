namespace PSPad.App.State;

public static class ReferenceQuery
{
    const string NewItem = "new";

    public static Guid? From(string uri) =>
        Guid.TryParse(QueryString.Value(uri, "item"), out var id) ? id : null;

    public static Guid? NewItemListFrom(string uri) =>
        QueryString.Value(uri, "item") == NewItem && Guid.TryParse(QueryString.Value(uri, "list"), out var listId)
            ? listId
            : null;

    public static string ForItem(string uri, Guid itemId) => $"{QueryString.Without(uri)}?item={itemId}";

    public static string ForNewItem(string uri, Guid listId) => $"{QueryString.Without(uri)}?item={NewItem}&list={listId}";
}
