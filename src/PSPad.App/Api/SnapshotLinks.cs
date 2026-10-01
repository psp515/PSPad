namespace PSPad.App.Api;

public static class SnapshotLinks
{
    public static string For(string baseUri, string token) => $"{baseUri.TrimEnd('/')}/s/{token}";
}
