using System.Globalization;

namespace PSPad.App.Components;

public static class OpenTaskCount
{
    public static string Describe(int open) => open switch
    {
        0 => "no open tasks",
        1 => "1 open task",
        _ => $"{open.ToString(CultureInfo.InvariantCulture)} open tasks"
    };
}
