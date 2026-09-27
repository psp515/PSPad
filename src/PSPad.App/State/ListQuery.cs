namespace PSPad.App.State;

public static class ListQuery
{
    const string NewList = "new";

    public static Guid? NewListAreaFrom(string uri) =>
        QueryString.Value(uri, "list") == NewList && Guid.TryParse(QueryString.Value(uri, "inarea"), out var areaId)
            ? areaId
            : null;

    public static string ForNewList(string uri, Guid areaId) =>
        $"{QueryString.Without(uri)}?list={NewList}&inarea={areaId}";
}
