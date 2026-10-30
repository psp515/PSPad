using PSPad.Module.Investments.Operations;

namespace PSPad.App.State.Investments;

public static class OperationKinds
{
    public static IReadOnlyList<OperationKind> All { get; } = Enum.GetValues<OperationKind>();

    public static string Key(OperationKind kind) => $"Kind{kind}";

    public static bool Traded(OperationKind kind) => kind is OperationKind.Buy or OperationKind.Sell;

    public static bool HasTicker(OperationKind kind) => Traded(kind) || kind is OperationKind.Dividend or OperationKind.Tax;
}
