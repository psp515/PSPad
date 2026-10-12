namespace PSPad.Backup;

public sealed class BackupFailure(string message) : Exception(message);
