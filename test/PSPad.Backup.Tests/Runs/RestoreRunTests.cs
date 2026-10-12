using Microsoft.Extensions.Time.Testing;
using PSPad.Backup.Configuration;
using PSPad.Backup.Health;
using PSPad.Backup.Runs;
using PSPad.Backup.Tests.Fakes;
using PSPad.TestInfrastructure;

namespace PSPad.Backup.Tests.Runs;

[UnitTest]
public sealed class RestoreRunTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("restore-run-").FullName;
    readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 12, 3, 17, 0, TimeSpan.Zero));
    readonly FakeStore _local = new("local");
    readonly FakeStore _s3 = new("s3");
    readonly FakeEncryptor _encryptor = new();
    readonly StringWriter _log = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    string Identity(string contents = "AGE-SECRET-KEY-1")
    {
        var path = Path.Combine(_root, "identity");
        File.WriteAllText(path, contents);
        return path;
    }

    async Task BackUp(string data)
    {
        var run = new BackupRun(new BackupPolicy(BackupStack.Mongo, 14, true), new FakeDump { Data = data }, _local, [_s3],
            _encryptor, new HealthStamps(Path.Combine(_root, "a"), Path.Combine(_root, "b"), _clock), new FakePing(),
            _clock, _root, new BackupLog(TextWriter.Null));
        await run.RunAsync(TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromDays(1));
    }

    RestoreRun Restore(FakeDump dump, bool accepting = false, bool confirm = true, string? identity = null) =>
        new(BackupStack.Mongo, dump, _local, [_s3], _encryptor, new FakeProbe(accepting), "api:8080",
            identity ?? Identity(), new FakeConfirmation(confirm), _root, new BackupLog(_log));

    [Fact]
    public async Task LatestRestoresTheNewestLocalBackup()
    {
        await BackUp("monday");
        await BackUp("tuesday");
        var dump = new FakeDump();

        await Restore(dump).RestoreAsync(new RestoreRequest("latest", "local", Confirmed: false), TestContext.Current.CancellationToken);

        Assert.Equal("tuesday", dump.Restored);
        Assert.Contains("restored mongo-2026-10-13T031700Z.tar", _log.ToString());
    }

    [Fact]
    public async Task ANamedBackupIsRestoredFromARemoteWithTheIdentity()
    {
        await BackUp("monday");
        await BackUp("tuesday");
        var dump = new FakeDump();
        var identity = Identity();

        await Restore(dump, identity: identity).RestoreAsync(
            new RestoreRequest("mongo-2026-10-12T031700Z", "s3", Confirmed: true), TestContext.Current.CancellationToken);

        Assert.Equal("monday", dump.Restored);
        Assert.Equal(identity, _encryptor.IdentityUsed);
    }

    [Fact]
    public async Task ARestoreRefusesWhileTheWriterStillAcceptsConnections()
    {
        await BackUp("monday");
        var dump = new FakeDump();

        var failure = await Assert.ThrowsAsync<BackupFailure>(() =>
            Restore(dump, accepting: true).RestoreAsync(new RestoreRequest("latest", "local", true), TestContext.Current.CancellationToken));

        Assert.Contains("api:8080 is still accepting connections", failure.Message);
        Assert.Contains("docker compose stop api", failure.Message);
        Assert.Null(dump.Restored);
    }

    [Fact]
    public async Task ARemoteRestoreNeedsThePrivateKey()
    {
        await BackUp("monday");

        var failure = await Assert.ThrowsAsync<BackupFailure>(() =>
            Restore(new FakeDump(), identity: Identity("")).RestoreAsync(new RestoreRequest("latest", "s3", true), TestContext.Current.CancellationToken));

        Assert.Contains("BACKUP_AGE_IDENTITY_FILE", failure.Message);
    }

    [Fact]
    public async Task AnUnconfiguredSourceIsNamed()
    {
        var failure = await Assert.ThrowsAsync<BackupFailure>(() =>
            Restore(new FakeDump()).RestoreAsync(new RestoreRequest("latest", "gdrive", true), TestContext.Current.CancellationToken));

        Assert.Contains("'gdrive' is not configured", failure.Message);
    }

    [Fact]
    public async Task NoBackupsIsSaidPlainly()
    {
        var failure = await Assert.ThrowsAsync<BackupFailure>(() =>
            Restore(new FakeDump()).RestoreAsync(new RestoreRequest("latest", "local", true), TestContext.Current.CancellationToken));

        Assert.Contains("no backups on local", failure.Message);
    }

    [Fact]
    public async Task ADeclinedConfirmationChangesNothing()
    {
        await BackUp("monday");
        var dump = new FakeDump();

        var failure = await Assert.ThrowsAsync<BackupFailure>(() =>
            Restore(dump, confirm: false).RestoreAsync(new RestoreRequest("latest", "local", false), TestContext.Current.CancellationToken));

        Assert.Contains("not confirmed", failure.Message);
        Assert.Null(dump.Restored);
    }
}
