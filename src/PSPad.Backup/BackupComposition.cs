using PSPad.Backup.Configuration;
using PSPad.Backup.Databases;
using PSPad.Backup.Encryption;
using PSPad.Backup.Health;
using PSPad.Backup.Processes;
using PSPad.Backup.Runs;
using PSPad.Backup.Storage;

namespace PSPad.Backup;

public sealed class BackupComposition(BackupSettings settings, TimeProvider clock, BackupLog log)
{
    readonly ProcessRunner _runner = new();
    IReadOnlyList<IBackupStore>? _remotes;

    public LocalStore Local { get; } = new(BackupSettings.BackupDirectory);

    public HealthStamps Health { get; } = new(BackupSettings.StartedStamp, BackupSettings.SuccessStamp, clock);

    public AgeEncryptor Encryptor => new(_runner, settings.AgeRecipient);

    public IReadOnlyList<IBackupStore> Remotes => _remotes ??= CreateRemotes();

    public async Task RequireRecipientAsync(CancellationToken ct)
    {
        if (settings.HasRemotes && settings.AgeRecipient is null)
        {
            throw new BackupFailure("BACKUP_AGE_RECIPIENT is required once S3 or Google Drive is set; off-host copies are never uploaded in plaintext");
        }

        if (settings.AgeRecipient is not null && !await Encryptor.AcceptsRecipientAsync(ct))
        {
            throw new BackupFailure("BACKUP_AGE_RECIPIENT is not an age recipient (age1…)");
        }
    }

    public BackupRun Backup() =>
        new(new BackupPolicy(settings.Stack, settings.KeepDays, settings.RemotePrune), Dump(), Local, Remotes,
            Encryptor, Health, new HttpPing(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, settings.PingUrl, log),
            clock, BackupSettings.BackupDirectory, log);

    public RestoreRun Restore() =>
        new(settings.Stack, Dump(), Local, Remotes, Encryptor, new TcpServiceProbe(), settings.ServiceProbe,
            BackupSettings.IdentityFile, new ConsoleConfirmation(), Path.GetTempPath(), log);

    IDatabaseDump Dump() => settings.Stack switch
    {
        BackupStack.Mongo => new MongoDump(settings.MongoUri!, settings.MongoDatabase!, _runner, log),
        _ => new PostgresDump(settings.PostgresUri!, _runner)
    };

    IReadOnlyList<IBackupStore> CreateRemotes()
    {
        var remotes = new List<IBackupStore>();
        if (settings.S3 is { } s3)
        {
            remotes.Add(S3Store.Create(s3, settings.Stack));
        }

        if (settings.Drive is { } drive)
        {
            remotes.Add(DriveStore.Create(drive, settings.Stack));
        }

        return remotes;
    }
}
