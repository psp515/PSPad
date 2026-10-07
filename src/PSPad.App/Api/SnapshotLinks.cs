namespace PSPad.App.Api;

public static class SnapshotLinks
{
    public static string Route(string token) => $"/public/snapshot/{token}";

    public static string For(string baseUri, string token) => $"{baseUri.TrimEnd('/')}{Route(token)}";
}
