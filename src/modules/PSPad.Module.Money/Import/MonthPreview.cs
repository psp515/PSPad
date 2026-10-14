using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Import;

public sealed record MonthPreview(
    YearMonth Month, bool IsNew, IReadOnlyList<HoldingChange> Changes, int Holdings, int Unchanged)
{
    public int Updates => Changes.Count(change => change.Kind == HoldingChangeKind.Edit);

    public int Adds => Changes.Count(change => change.Kind == HoldingChangeKind.Add);
}
