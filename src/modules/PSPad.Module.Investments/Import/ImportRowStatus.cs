namespace PSPad.Module.Investments.Import;

public enum ImportRowStatus
{
    ToImport,
    AlreadyImported,
    DeletedEarlier,
    Error,
    WaitingForRate
}
