using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text.Json;

namespace PSPad.Backup.Archives;

public static class BackupArchive
{
    const string Sums = "SHA256SUMS";
    const string Manifest = "manifest.json";

    public static async Task PackAsync(
        string content, string archive, string name, string stack, DateTimeOffset takenAt, CancellationToken ct)
    {
        var files = new List<(string Path, long Bytes, string Hash)>();
        foreach (var file in Directory.EnumerateFiles(content, "*", SearchOption.AllDirectories)
                     .Select(path => Path.GetRelativePath(content, path).Replace('\\', '/'))
                     .Order(StringComparer.Ordinal))
        {
            var full = Path.Combine(content, file);
            files.Add((file, new FileInfo(full).Length, await HashAsync(full, ct)));
        }

        await File.WriteAllLinesAsync(Path.Combine(content, Sums), files.Select(file => $"{file.Hash}  {file.Path}"), ct);
        await File.WriteAllTextAsync(Path.Combine(content, Manifest), JsonSerializer.Serialize(new
        {
            stack,
            name,
            takenAt,
            files = files.Select(file => new { name = file.Path, bytes = file.Bytes, sha256 = file.Hash })
        }), ct);

        await TarFile.CreateFromDirectoryAsync(content, archive, includeBaseDirectory: false, ct);
    }

    public static async Task UnpackAsync(string archive, string content, CancellationToken ct)
    {
        Directory.CreateDirectory(content);
        await TarFile.ExtractToDirectoryAsync(archive, content, overwriteFiles: false, ct);

        var sums = Path.Combine(content, Sums);
        if (!File.Exists(sums))
        {
            throw new BackupFailure($"{Path.GetFileName(archive)} has no {Sums}; not restoring it");
        }

        foreach (var line in await File.ReadAllLinesAsync(sums, ct))
        {
            var hash = line[..64];
            var file = line[66..];
            var full = Path.Combine(content, file);
            if (!File.Exists(full) || await HashAsync(full, ct) != hash)
            {
                throw new BackupFailure($"{file} failed its checksum; not restoring this backup");
            }
        }
    }

    static async Task<string> HashAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
    }
}
