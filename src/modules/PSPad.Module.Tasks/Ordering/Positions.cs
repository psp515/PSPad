using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Ordering;

public static class Positions
{
    public static int Next(IEnumerable<int> taken)
    {
        var positions = taken as IReadOnlyCollection<int> ?? taken.ToArray();
        return positions.Count == 0 ? 0 : positions.Max() + 1;
    }

    public static IReadOnlyList<Guid> Move(IReadOnlyList<Guid> order, Guid moved, int toIndex)
    {
        if (!order.Contains(moved))
        {
            throw new DomainRejectedException("The item being moved is not in this order.");
        }

        var remaining = order.Where(id => id != moved).ToList();
        var target = Math.Clamp(toIndex, 0, remaining.Count);
        remaining.Insert(target, moved);
        return remaining;
    }
}
