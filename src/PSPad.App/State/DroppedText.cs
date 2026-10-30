namespace PSPad.App.State;

public static class DroppedText
{
    public static string? Read(string? uriList, string? text) => FirstUri(uriList) ?? NonBlank(text);

    static string? FirstUri(string? uriList)
    {
        if (string.IsNullOrWhiteSpace(uriList))
        {
            return null;
        }

        foreach (var rawLine in uriList.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            return line;
        }

        return null;
    }

    static string? NonBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
