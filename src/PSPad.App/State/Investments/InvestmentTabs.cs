namespace PSPad.App.State.Investments;

public static class InvestmentTabs
{
    public const int Positions = 0;
    public const int Operations = 1;
    public const int Accounts = 2;

    static readonly string[] Names = ["positions", "operations", "accounts"];

    public static int IndexOf(string? tab) => Math.Max(Positions, Array.IndexOf(Names, tab?.ToLowerInvariant() ?? ""));

    public static string NameOf(int index) => Names[index];
}
