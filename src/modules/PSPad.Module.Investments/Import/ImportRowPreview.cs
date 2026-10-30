namespace PSPad.Module.Investments.Import;

public sealed record ImportRowPreview(
    XtbRow Row, ImportRowStatus Status, Guid? OperationId, DateOnly? Day, IReadOnlyList<string> Errors, bool Ticked);
