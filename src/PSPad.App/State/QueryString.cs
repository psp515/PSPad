namespace PSPad.App.State;

static class QueryString
{
    public static string Without(string uri) => uri.Split('?')[0];

    public static string? Value(string uri, string key)
    {
        var mark = uri.IndexOf('?');

        if (mark < 0)
        {
            return null;
        }

        foreach (var pair in uri[(mark + 1)..].Split('&'))
        {
            var parts = pair.Split('=', 2);

            if (parts.Length == 2 && parts[0] == key)
            {
                return parts[1];
            }
        }

        return null;
    }
}
