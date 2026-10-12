using System.Formats.Tar;
using System.Text.Json;
using PSPad.Backup.Archives;
using PSPad.TestInfrastructure;

namespace PSPad.Backup.Tests.Archives;

[UnitTest]
public sealed class BackupArchiveTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("backup-archive-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    string Content()
    {
        var content = Path.Combine(_root, "content");
        Directory.CreateDirectory(Path.Combine(content, "mongo", "pspad"));
        File.WriteAllText(Path.Combine(content, "mongo", "pspad", "areas.bson.gz"), "areas");
        File.WriteAllText(Path.Combine(content, "mongo", "oplog.bson"), "oplog");
        return content;
    }

    [Fact]
    public async Task APackedArchiveUnpacksAndVerifies()
    {
        var tar = Path.Combine(_root, "mongo.tar");
        await BackupArchive.PackAsync(Content(), tar, "mongo-2026-10-12T193217Z", "mongo",
            new DateTimeOffset(2026, 10, 12, 19, 32, 17, TimeSpan.Zero), TestContext.Current.CancellationToken);

        var unpacked = Path.Combine(_root, "unpacked");
        await BackupArchive.UnpackAsync(tar, unpacked, TestContext.Current.CancellationToken);

        Assert.Equal("areas", File.ReadAllText(Path.Combine(unpacked, "mongo", "pspad", "areas.bson.gz")));
    }

    [Fact]
    public async Task TheManifestNamesEveryFileWithItsSizeAndHash()
    {
        var content = Content();
        await BackupArchive.PackAsync(content, Path.Combine(_root, "mongo.tar"), "mongo-2026-10-12T193217Z", "mongo",
            new DateTimeOffset(2026, 10, 12, 19, 32, 17, TimeSpan.Zero), TestContext.Current.CancellationToken);

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(content, "manifest.json")));
        var files = manifest.RootElement.GetProperty("files").EnumerateArray()
            .Select(file => file.GetProperty("name").GetString())
            .ToArray();

        Assert.Equal("mongo", manifest.RootElement.GetProperty("stack").GetString());
        Assert.Equal(["mongo/oplog.bson", "mongo/pspad/areas.bson.gz"], files);
    }

    [Fact]
    public async Task ATamperedFileFailsVerification()
    {
        var tar = Path.Combine(_root, "mongo.tar");
        await BackupArchive.PackAsync(Content(), tar, "mongo-x", "mongo", DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);
        var tampered = Directory.CreateDirectory(Path.Combine(_root, "tampered")).FullName;
        await TarFile.ExtractToDirectoryAsync(tar, tampered, overwriteFiles: false, TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(tampered, "mongo", "oplog.bson"), "changed");
        var repacked = Path.Combine(_root, "tampered.tar");
        await TarFile.CreateFromDirectoryAsync(tampered, repacked, includeBaseDirectory: false, TestContext.Current.CancellationToken);

        var failure = await Assert.ThrowsAsync<BackupFailure>(() =>
            BackupArchive.UnpackAsync(repacked, Path.Combine(_root, "out"), TestContext.Current.CancellationToken));

        Assert.Contains("mongo/oplog.bson", failure.Message);
    }
}
