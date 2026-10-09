using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public static class CategoryName
{
    public const int MaxLength = 40;

    public static string Normalize(string name)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainRejectedException("A category needs a name.");
        }

        return trimmed.Length > MaxLength
            ? throw new DomainRejectedException($"A category name is at most {MaxLength} characters.")
            : trimmed;
    }

    public static int IndexIn(IReadOnlyList<string> names, string name)
    {
        for (var index = 0; index < names.Count; index++)
        {
            if (string.Equals(names[index], name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
