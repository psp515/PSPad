using PSPad.Backup.Processes;

namespace PSPad.Backup.Databases;

public sealed class PostgresDump(string uri, IProcessRunner runner) : IDatabaseDump
{
    static readonly Dictionary<string, string> Environment = new() { ["PGCONNECT_TIMEOUT"] = "10" };

    public string ConfirmationWord => "keycloak";

    public async Task DumpAsync(string directory, CancellationToken ct)
    {
        await RequirePostgresAsync(ct);
        await runner.RunAsync("pg_dump",
            ["--format=custom", $"--file={Path.Combine(directory, "keycloak.dump")}", uri], Environment, ct);
    }

    public async Task RestoreAsync(string directory, CancellationToken ct)
    {
        await RequirePostgresAsync(ct);
        await runner.RunAsync("pg_restore",
            ["--clean", "--if-exists", "--single-transaction", $"--dbname={uri}", Path.Combine(directory, "keycloak.dump")],
            Environment, ct);
    }

    async Task RequirePostgresAsync(CancellationToken ct)
    {
        try
        {
            await runner.RunAsync("psql", [uri, "-Atqc", "select 1"], Environment, ct);
        }
        catch (BackupFailure)
        {
            throw new BackupFailure(
                "BACKUP_PG_URI is not a reachable Postgres; keycloak-backup only supports Keycloak on Postgres (KC_DB=postgres)");
        }
    }
}
