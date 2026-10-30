using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public static class TaskTick
{
    public static bool Toggles(TodoTask task, DateOnly day) =>
        !task.IsRecurring || task.OccursOn(day) || task.CompletedDays.Contains(day);
}
