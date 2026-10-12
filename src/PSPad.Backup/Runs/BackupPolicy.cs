using PSPad.Backup.Configuration;

namespace PSPad.Backup.Runs;

public sealed record BackupPolicy(BackupStack Stack, int KeepDays, bool RemotePrune);
