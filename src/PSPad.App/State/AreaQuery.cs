namespace PSPad.App.State;

public static class AreaQuery
{
    const string NewArea = "new";

    public static Guid? From(string uri) =>
        Guid.TryParse(QueryString.Value(uri, "area"), out var id) ? id : null;

    public static bool IsNew(string uri) => QueryString.Value(uri, "area") == NewArea;

    public static string ForNew(string uri) => $"{QueryString.Without(uri)}?area={NewArea}";

    public static string For(string uri, Guid areaId) => $"{QueryString.Without(uri)}?area={areaId}";
}
