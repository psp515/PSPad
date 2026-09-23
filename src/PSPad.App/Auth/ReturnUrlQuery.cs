namespace PSPad.App.Auth;

public static class ReturnUrlQuery
{
    const string Name = "returnUrl";

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
}
