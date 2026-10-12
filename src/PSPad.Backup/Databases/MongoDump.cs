using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PSPad.Backup.Processes;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Backup.Databases;

public sealed partial class MongoDump(string uri, string database, IProcessRunner runner, BackupLog log) : IDatabaseDump
{
    public string ConfirmationWord => database;

    public async Task DumpAsync(string directory, CancellationToken ct)
    {
        var target = Path.Combine(directory, "mongo");
        await runner.RunAsync("mongodump", ["--uri", uri, "--oplog", "--gzip", $"--out={target}", "--quiet"], null, ct);

        // Restoring admin would swap the user the restore is signed in as, and the oplog replay would then be refused.
        foreach (var system in new[] { "admin", "config" }.Select(name => Path.Combine(target, name)).Where(Directory.Exists))
        {
            Directory.Delete(system, recursive: true);
        }
    }

    public async Task RestoreAsync(string directory, CancellationToken ct)
    {
        // --oplogReplay refuses namespace filters, so the database is emptied first:
        // collections created after the backup would otherwise survive the restore.
        await new MongoClient(uri).DropDatabaseAsync(database, ct);

        var result = await runner.RunAsync("mongorestore",
            ["--uri", uri, "--oplogReplay", "--drop", "--gzip", $"--dir={Path.Combine(directory, "mongo")}"], null, ct);
        if (RestoredCount().Match(result.Errors + result.Output) is { Success: true } restored)
        {
            log.Info(restored.Value);
        }

        await new DatasetIdentity(new MongoContext(Options.Create(new MongoOptions
        {
            ConnectionString = uri,
            Database = database
        }))).ForgetAsync(ct);
    }

    [GeneratedRegex(@"\d+ document\(s\) restored successfully")]
    private static partial Regex RestoredCount();
}
