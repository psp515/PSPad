using System.Globalization;
using System.Resources;
using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Reminders;

public static class ReminderTexts
{
    private static readonly ResourceManager Strings =
        new("PSPad.Module.Reminders.Resources.ReminderStrings", typeof(ReminderTexts).Assembly);

    public static PushNote For(TodoTask task, int daysBefore, string language, string appBaseAddress)
    {
        var culture = CultureOf(language);
        var body = daysBefore switch
        {
            0 => Strings.GetString("DueToday", culture)!,
            1 => Strings.GetString("DueTomorrow", culture)!,
            _ => string.Format(culture, Strings.GetString("DueInDays", culture)!, daysBefore),
        };
        return new PushNote(task.Name, body, $"{appBaseAddress.TrimEnd('/')}/?task={task.Id}", task.Id.ToString());
    }

    public static PushNote Test(string language, string appBaseAddress)
    {
        var culture = CultureOf(language);
        return new PushNote(
            Strings.GetString("TestTitle", culture)!,
            Strings.GetString("TestBody", culture)!,
            $"{appBaseAddress.TrimEnd('/')}/",
            "pspad-test");
    }

    private static CultureInfo CultureOf(string language) =>
        CultureInfo.GetCultureInfo(SupportedLanguages.IsSupported(language) ? language : SupportedLanguages.Default);
}
