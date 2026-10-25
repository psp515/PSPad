using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;

namespace PSPad.Module.Tasks.Lists;

public sealed record ListSort(ListOrder Order, bool Descending)
{
    public static ListSort Default { get; } = new(ListOrder.Date, false);

    public IReadOnlyList<TodoTask> Apply(IEnumerable<TodoTask> tasks, DateOnly today, StringComparer names)
    {
        var dated = tasks.Select(task => (Task: task, Date: TodayRule.DateOf(task, today))).ToArray();

        var ordered = Order switch
        {
            ListOrder.Date => ByDate(dated)
                .ThenByDescending(entry => entry.Task.Priority)
                .ThenBy(entry => entry.Task.Name, names),
            ListOrder.Priority => ByPriority(dated)
                .ThenBy(entry => entry.Date is null)
                .ThenBy(entry => entry.Date)
                .ThenBy(entry => entry.Task.Name, names),
            ListOrder.Name => Descending
                ? dated.OrderByDescending(entry => entry.Task.Name, names)
                : dated.OrderBy(entry => entry.Task.Name, names),
            _ => ByAdded(dated)
                .ThenBy(entry => entry.Task.Name, names)
        };

        return [.. ordered.ThenBy(entry => entry.Task.Id).Select(entry => entry.Task)];
    }

    IOrderedEnumerable<(TodoTask Task, DateOnly? Date)> ByDate(IEnumerable<(TodoTask Task, DateOnly? Date)> dated)
    {
        var undatedLast = dated.OrderBy(entry => entry.Date is null);
        return Descending ? undatedLast.ThenByDescending(entry => entry.Date) : undatedLast.ThenBy(entry => entry.Date);
    }

    IOrderedEnumerable<(TodoTask Task, DateOnly? Date)> ByPriority(IEnumerable<(TodoTask Task, DateOnly? Date)> dated) =>
        Descending ? dated.OrderBy(entry => entry.Task.Priority) : dated.OrderByDescending(entry => entry.Task.Priority);

    IOrderedEnumerable<(TodoTask Task, DateOnly? Date)> ByAdded(IEnumerable<(TodoTask Task, DateOnly? Date)> dated)
    {
        var unknownLast = dated.OrderBy(entry => entry.Task.CreatedAt is null);
        return Descending ? unknownLast.ThenBy(entry => entry.Task.CreatedAt) : unknownLast.ThenByDescending(entry => entry.Task.CreatedAt);
    }
}
