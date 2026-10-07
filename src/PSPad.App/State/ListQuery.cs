namespace PSPad.App.State;

public static class ListQuery
{
    const string NewList = "new";

    public static Guid? NewListAreaFrom(string uri) =>
        QueryString.Value(uri, "list") == NewList && Guid.TryParse(QueryString.Value(uri, "inarea"), out var areaId)
            ? areaId
            : null;

    // New-task and new-item URLs also carry list=, naming the target list rather than opening it.
    public static Guid? From(string uri) =>
        QueryString.Value(uri, "task") is null && QueryString.Value(uri, "item") is null &&
        Guid.TryParse(QueryString.Value(uri, "list"), out var id)
            ? id
            : null;

    public static string For(string uri, Guid listId) => $"{QueryString.Without(uri)}?list={listId}";

    public static ListPanelView ViewFrom(string uri) => QueryString.Value(uri, "view") switch
    {
        "members" => ListPanelView.Members,
        "snapshots" => ListPanelView.Snapshots,
        _ => ListPanelView.Details
    };

    public static string For(string uri, Guid listId, ListPanelView view) => view switch
    {
        ListPanelView.Members => $"{For(uri, listId)}&view=members",
        ListPanelView.Snapshots => $"{For(uri, listId)}&view=snapshots",
        _ => For(uri, listId)
    };

    public static string ForNewList(string uri, Guid areaId) =>
        $"{QueryString.Without(uri)}?list={NewList}&inarea={areaId}";
}
