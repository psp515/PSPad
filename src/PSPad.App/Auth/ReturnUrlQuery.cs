namespace PSPad.App.Auth;

public static class ReturnUrlQuery
{
    const string Name = "returnUrl";

    static readonly string[] SignInPaths = ["welcome", "authentication/"];

    public static string? From(string uri)
    {
        var query = new Uri(uri).Query.TrimStart('?');

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');

            if (separator > 0 && pair[..separator] == Name && separator + 1 < pair.Length)
            {
                return Uri.UnescapeDataString(pair[(separator + 1)..]);
            }
        }

        return null;
    }

    public static string? SafeFrom(string uri, string baseUri)
    {
        var returnUrl = From(uri);

        if (returnUrl is null || returnUrl.Contains('\\') || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        var root = new Uri(baseUri);
        var parsed = !returnUrl.StartsWith('/') && Uri.TryCreate(returnUrl, UriKind.Absolute, out var resolved)
            || Uri.TryCreate(root, returnUrl.TrimStart('/'), out resolved);

        if (!parsed || resolved is null || resolved.Scheme != root.Scheme || resolved.Authority != root.Authority || !root.IsBaseOf(resolved))
        {
            return null;
        }

        var path = root.MakeRelativeUri(resolved).OriginalString;

        return SignInPaths.Any(signIn => path.StartsWith(signIn, StringComparison.OrdinalIgnoreCase))
            ? null
            : resolved.AbsoluteUri;
    }
}
