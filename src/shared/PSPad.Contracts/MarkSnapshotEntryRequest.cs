namespace PSPad.Contracts;

public sealed record MarkSnapshotEntryRequest(Guid EntryId, Guid? StepId, bool Marked);
