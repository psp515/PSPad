using PSPad.Backup.Archives;
using PSPad.Backup.Configuration;
using PSPad.Backup.Databases;
using PSPad.Backup.Encryption;
using PSPad.Backup.Storage;

namespace PSPad.Backup.Runs;

public sealed class RestoreRun(
    BackupStack stack,
    IDatabaseDump dump,
    IBackupStore local,
    IReadOnlyList<IBackupStore> remotes,
    IEncryptor? encryptor,
    IServiceProbe probe,
    string? probeTarget,
    string identityFile,
    IConfirmation confirmation,
    string workRoot,
    BackupLog log)
{
    public async Task RestoreAsync(RestoreRequest request, CancellationToken ct)
    {
        await RefuseWhileTheWriterListensAsync(ct);

        var store = request.From == local.Name
            ? local
            : remotes.FirstOrDefault(remote => remote.Name == request.From)
              ?? throw new BackupFailure($"'{request.From}' is not configured; restore from local, s3 or gdrive");
        var encrypted = store != local;

        if (encrypted && (!File.Exists(identityFile) || new FileInfo(identityFile).Length == 0))
        {
            throw new BackupFailure($"restoring from {store.Name} needs the age private key: set BACKUP_AGE_IDENTITY_FILE");
        }

        var file = await ResolveAsync(store, request.Backup, encrypted, ct);
        var work = Directory.CreateDirectory(Path.Combine(workRoot, $".restore-{Guid.NewGuid():N}")).FullName;

        try
        {
            var archive = Path.Combine(work, "backup.tar");
            if (encrypted)
            {
                var download = Path.Combine(work, "backup.tar.age");
                await store.FetchAsync(file, download, ct);
                await (encryptor ?? throw new BackupFailure("age is not available to decrypt this backup"))
                    .DecryptAsync(download, archive, identityFile, ct);
            }
            else
            {
                await store.FetchAsync(file, archive, ct);
            }

            var content = Path.Combine(work, "content");
            await BackupArchive.UnpackAsync(archive, content, ct);
            log.Info($"verified {file}");

            if (!request.Confirmed && !confirmation.Confirm(dump.ConfirmationWord, file))
            {
                throw new BackupFailure("not confirmed; nothing changed");
            }

            await dump.RestoreAsync(content, ct);
            log.Info($"restored {file}");
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    async Task RefuseWhileTheWriterListensAsync(CancellationToken ct)
    {
        if (probeTarget is not null && await probe.IsAcceptingAsync(probeTarget, ct))
        {
            var service = probeTarget.Split(':')[0];
            throw new BackupFailure($"{probeTarget} is still accepting connections; stop it first: docker compose stop {service}");
        }
    }

    async Task<string> ResolveAsync(IBackupStore store, string backup, bool encrypted, CancellationToken ct)
    {
        var available = (await store.ListAsync(ct))
            .Where(file => BackupName.TryParse(stack, file, out _) && file.EndsWith(encrypted ? ".tar.age" : ".tar", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (backup == "latest")
        {
            return available.LastOrDefault() ?? throw new BackupFailure($"no backups on {store.Name}");
        }

        if (!BackupName.TryParse(stack, backup, out var name))
        {
            throw new BackupFailure($"'{backup}' is not a {stack.ToString().ToLowerInvariant()} backup name; see the list command");
        }

        var wanted = encrypted ? name.Encrypted : name.Archive;
        return available.Contains(wanted) ? wanted : throw new BackupFailure($"no backup {wanted} on {store.Name}");
    }
}
