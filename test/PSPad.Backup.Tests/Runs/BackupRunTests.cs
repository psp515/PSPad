using Microsoft.Extensions.Time.Testing;
using PSPad.Backup.Configuration;
using PSPad.Backup.Health;
using PSPad.Backup.Runs;
using PSPad.Backup.Tests.Fakes;
using PSPad.TestInfrastructure;

namespace PSPad.Backup.Tests.Runs;

[UnitTest]
public sealed class BackupRunTests : IDisposable
{
    const string Taken = "mongo-2026-10-12T031700Z";
    readonly string _root = Directory.CreateTempSubdirectory("backup-run-").FullName;
    readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 12, 3, 17, 0, TimeSpan.Zero));
    readonly FakeStore _local = new("local");
    readonly FakePing _ping = new();
    readonly StringWriter _log = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    HealthStamps Health() => new(Path.Combine(_root, "started"), Path.Combine(_root, "success"), _clock);

    BackupRun Run(FakeDump dump, IReadOnlyList<FakeStore> remotes, bool remotePrune = true) =>
        new(new BackupPolicy(BackupStack.Mongo, KeepDays: 14, remotePrune), dump, _local, remotes,
            remotes.Count > 0 ? new FakeEncryptor() : null, Health(), _ping, _clock, _root, new BackupLog(_log));

    [Fact]
    public async Task ARunKeepsAPlainCopyAndUploadsOnlyCiphertext()
    {
        var s3 = new FakeStore("s3");
        var drive = new FakeStore("gdrive");

        await Run(new FakeDump(), [s3, drive]).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"{Taken}.tar"], _local.Files.Keys);
        Assert.Equal([$"{Taken}.tar.age"], s3.Files.Keys);
        Assert.True(FakeEncryptor.IsEncrypted(drive.Files[$"{Taken}.tar.age"]));
        Assert.True(Health().IsHealthy());
        Assert.Equal([true], _ping.Sent);
        Assert.Empty(Directory.GetDirectories(_root));
    }

    [Fact]
    public async Task AFailedUploadStillTriesTheOtherRemoteFailsTheRunAndPrunesNothing()
    {
        _local.Files["mongo-2026-01-01T000000Z.tar"] = [1];
        var s3 = new FakeStore("s3") { FailUploads = true };
        var drive = new FakeStore("gdrive");

        var failure = await Assert.ThrowsAsync<BackupFailure>(() => Run(new FakeDump(), [s3, drive]).RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains("nothing was pruned", failure.Message);
        Assert.Contains($"{Taken}.tar.age", drive.Files.Keys);
        Assert.Contains("mongo-2026-01-01T000000Z.tar", _local.Files.Keys);
        Assert.Contains("upload to s3 failed", _log.ToString());
        Assert.False(Health().IsHealthy());
        Assert.Equal([false], _ping.Sent);
    }

    [Fact]
    public async Task ASuccessfulRunPrunesExpiredBackupsEverywhere()
    {
        var s3 = new FakeStore("s3");
        _local.Files["mongo-2026-01-01T000000Z.tar"] = [1];
        _local.Files["keep-me.tar"] = [1];
        s3.Files["mongo-2026-01-01T000000Z.tar.age"] = [1];

        await Run(new FakeDump(), [s3]).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["keep-me.tar", $"{Taken}.tar"], _local.Files.Keys.Order());
        Assert.Equal([$"{Taken}.tar.age"], s3.Files.Keys);
    }

    [Fact]
    public async Task RemotePruningCanBeLeftToTheBucket()
    {
        var s3 = new FakeStore("s3");
        s3.Files["mongo-2026-01-01T000000Z.tar.age"] = [1];

        await Run(new FakeDump(), [s3], remotePrune: false).RunAsync(TestContext.Current.CancellationToken);

        Assert.Contains("mongo-2026-01-01T000000Z.tar.age", s3.Files.Keys);
    }

    [Fact]
    public async Task AFailedDumpLeavesNothingBehindAndPingsTheFailure()
    {
        await Assert.ThrowsAsync<BackupFailure>(() => Run(new FakeDump { FailDump = true }, []).RunAsync(TestContext.Current.CancellationToken));

        Assert.Empty(_local.Files);
        Assert.Empty(Directory.GetDirectories(_root));
        Assert.Equal([false], _ping.Sent);
    }
}
