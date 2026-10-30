namespace PSPad.App.State;

public static class BudgetTabs
{
    public const int Overview = 0;
    public const int Entries = 1;
    public const int Balance = 2;
    public const int Stats = 3;
    public const int Settings = 4;

    static readonly string[] Names = ["overview", "entries", "balance", "stats", "settings"];
    static readonly Dictionary<string, string> Aliases = new() { ["month"] = "entries", ["summary"] = "stats" };

    public static int IndexOf(string? tab)
    {
        var name = tab?.ToLowerInvariant() ?? "";
        return Math.Max(Overview, Array.IndexOf(Names, Aliases.GetValueOrDefault(name, name)));
    }

    public static string NameOf(int index) => Names[index];
}
