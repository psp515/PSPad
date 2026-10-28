using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public static class TaskDone
{
    public static bool On(TodoTask task, DateOnly day) =>
        task.CompletedAt is not null || task.CompletedDays.Contains(day) || task.EndedBy(day);
}
