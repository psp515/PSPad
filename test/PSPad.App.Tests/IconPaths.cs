using System.Text.RegularExpressions;

namespace PSPad.App.Tests;

public static partial class IconPaths
{
    public static string DistinctivePath(string icon) =>
        DAttribute().Matches(icon).Select(match => match.Groups[1].Value)
            .OrderByDescending(path => path.Length)
            .First();

    [GeneratedRegex("d=\"([^\"]+)\"")]
    private static partial Regex DAttribute();
}
