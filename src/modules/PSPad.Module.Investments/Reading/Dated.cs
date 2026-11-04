namespace PSPad.Module.Investments.Reading;

internal static class Dated
{
    public static T? LastOnOrBefore<T>(IReadOnlyList<T> byDate, DateOnly day, Func<T, DateOnly> dateOf) where T : class
    {
        var low = 0;
        var high = byDate.Count - 1;
        T? found = null;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (dateOf(byDate[middle]) <= day)
            {
                found = byDate[middle];
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }
}
