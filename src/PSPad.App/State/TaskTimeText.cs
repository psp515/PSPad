using System.Globalization;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public static class TaskTimeText
{
    public const string NextDay = "(+1)";

    public static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Describe(TaskTime time) =>
        time.End is { } end
            ? $"{Format(time.Start)}–{Format(end)}{(time.Overnight ? $" {NextDay}" : "")}"
            : Format(time.Start);
}
