using PSPad.Module.Money.Budgets;

namespace PSPad.App.State;

public static class CategoryChoices
{
    public static IReadOnlyList<string> Search(IReadOnlyList<string> names, string? text)
    {
        var typed = text?.Trim() ?? "";

        if (typed.Length == 0)
        {
            return names;
        }

        var choices = names.Where(name => name.Contains(typed, StringComparison.OrdinalIgnoreCase)).ToList();

        if (IsNew(names, typed))
        {
            choices.Add(typed);
        }

        return choices;
    }

    public static bool IsNew(IReadOnlyList<string> names, string choice) => CategoryName.IndexIn(names, choice) < 0;
}
