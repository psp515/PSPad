using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskReminder(int DaysBefore, TimeOnly At)
{
    public const int MostDaysBefore = 99;

    public static TaskReminder Of(int daysBefore, TimeOnly at)
    {
        var reminder = new TaskReminder(daysBefore, at);
        reminder.Validate();
        return reminder;
    }

    public void Validate()
    {
        if (DaysBefore is < 0 or > MostDaysBefore)
        {
            throw new DomainRejectedException("A reminder must be between 0 and 99 days before.");
        }
    }
}
