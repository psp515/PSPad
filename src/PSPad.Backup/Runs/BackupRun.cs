using PSPad.Backup.Archives;
using PSPad.Backup.Databases;
using PSPad.Backup.Encryption;
using PSPad.Backup.Health;
using PSPad.Backup.Storage;

namespace PSPad.Backup.Runs;

public sealed class BackupRun(
    BackupPolicy policy,
    IDatabaseDump dump,
    IBackupStore local,
    IReadOnlyList<IBackupStore> remotes,
    IEncryptor? encryptor,
    HealthStamps health,
    IPing ping,
    TimeProvider clock,
    string workRoot,
    BackupLog log)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var name = BackupName.For(policy.Stack, clock.GetUtcNow());
        var work = Directory.CreateDirectory(Path.Combine(workRoot, $".work-{name.Value}")).FullName;

        try
        {
            var content = Directory.CreateDirectory(Path.Combine(work, "content")).FullName;
            log.Info($"dumping {policy.Stack.ToString().ToLowerInvariant()}");
            await dump.DumpAsync(content, ct);

            var archive = Path.Combine(work, name.Archive);
            await BackupArchive.PackAsync(content, archive, name.Value, policy.Stack.ToString().ToLowerInvariant(), name.TakenAt, ct);
            await local.PutAsync(archive, name.Archive, ct);
            log.Info($"kept {name.Archive} ({new FileInfo(archive).Length} bytes)");

            await UploadAsync(archive, name, work, ct);
            await PruneAsync(ct);

            health.MarkSucceeded();
            await ping.PingAsync(succeeded: true, ct);
            log.Info($"done: {name.Value}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log.Info("run failed");
            await ping.PingAsync(succeeded: false, ct);
            throw;
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    async Task UploadAsync(string archive, BackupName name, string work, CancellationToken ct)
    {
        if (remotes.Count == 0)
        {
            return;
        }

        var encrypted = Path.Combine(work, name.Encrypted);
        await (encryptor ?? throw new BackupFailure("BACKUP_AGE_RECIPIENT is required once S3 or Google Drive is set"))
            .EncryptAsync(archive, encrypted, ct);

        var failed = false;
        foreach (var remote in remotes)
        {
            try
            {
                await remote.PutAsync(encrypted, name.Encrypted, ct);
                log.Info($"uploaded to {remote.Name}");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                log.Info($"upload to {remote.Name} failed: {exception.Message}");
                failed = true;
            }
        }

        if (failed)
        {
            throw new BackupFailure("at least one upload failed; nothing was pruned");
        }
    }

    async Task PruneAsync(CancellationToken ct)
    {
        IEnumerable<IBackupStore> stores = policy.RemotePrune ? [local, .. remotes] : [local];
        foreach (var store in stores)
        {
            var expired = RetentionPolicy.Expired(policy.Stack, await store.ListAsync(ct), clock.GetUtcNow(), policy.KeepDays);
            foreach (var file in expired)
            {
                await store.DeleteAsync(file, ct);
                log.Info($"pruned {file} from {store.Name}");
            }
        }
    }
}
