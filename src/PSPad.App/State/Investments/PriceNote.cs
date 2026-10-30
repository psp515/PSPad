namespace PSPad.App.State.Investments;

public readonly record struct PriceNote(PriceNoteKind Kind, DateOnly? Day = null, DateTimeOffset? At = null)
{
    public static readonly PriceNote None = new(PriceNoteKind.None);
}
