namespace PSPad.Module.Money.Import;

public enum ImportStatus
{
    ToImport,
    Error,
    AlreadyInBudget,
    Unchanged,
    WaitingForRate
}
