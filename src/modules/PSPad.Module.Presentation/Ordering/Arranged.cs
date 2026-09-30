namespace PSPad.Module.Presentation.Ordering;

public static class Arranged
{
    public static IReadOnlyList<T> Sort<T>(
        IEnumerable<T> elements,
        IReadOnlyList<Guid> order,
        Func<T, Guid> id,
        Func<T, DateTimeOffset?> createdAt)
    {
        var rank = new Dictionary<Guid, int>();

        for (var index = 0; index < order.Count; index++)
        {
            rank.TryAdd(order[index], index);
        }

        return elements
            .OrderBy(element => rank.TryGetValue(id(element), out var index) ? index : int.MaxValue)
            .ThenBy(element => createdAt(element) ?? DateTimeOffset.MinValue)
            .ThenBy(id)
            .ToArray();
    }
}
