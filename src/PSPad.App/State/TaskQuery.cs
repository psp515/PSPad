namespace PSPad.App.State;

public static class TaskQuery
{
    public static Guid? From(string uri)
    {
        var mark = uri.IndexOf('?');

        if (mark < 0)
        {
            return null;
        }

        foreach (var pair in uri[(mark + 1)..].Split('&'))
        {
            var parts = pair.Split('=', 2);

            if (parts.Length == 2 && parts[0] == "task" && Guid.TryParse(parts[1], out var id))
            {
                return id;
            }
        }

        return null;
    }

    public static string Without(string uri) => uri.Split('?')[0];
}
