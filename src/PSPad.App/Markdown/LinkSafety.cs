namespace PSPad.App.Markdown;

public static class LinkSafety
{
    public static readonly string[] LinkSchemes = ["http", "https", "mailto"];

    public static bool IsSafe(string? url, string[] schemes) =>
        url is not null &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        schemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
}
