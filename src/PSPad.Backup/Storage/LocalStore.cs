namespace PSPad.Backup.Storage;

public sealed class LocalStore(string directory) : IBackupStore
{
    public string Name => "local";

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(Directory.EnumerateFiles(directory, "*.tar")
            .Select(path => Path.GetFileName(path))
            .ToArray());

    public async Task PutAsync(string source, string fileName, CancellationToken ct)
    {
        var partial = Path.Combine(directory, $".{fileName}.partial");
        await using (var from = File.OpenRead(source))
        await using (var to = File.Create(partial))
        {
            await from.CopyToAsync(to, ct);
        }

        File.Move(partial, Path.Combine(directory, fileName), overwrite: true);
    }

    public async Task FetchAsync(string fileName, string destination, CancellationToken ct)
    {
        var source = Path.Combine(directory, fileName);
        if (!File.Exists(source))
        {
            throw new BackupFailure($"no such local backup: {fileName}");
        }

        await using var from = File.OpenRead(source);
        await using var to = File.Create(destination);
        await from.CopyToAsync(to, ct);
    }

    public Task DeleteAsync(string fileName, CancellationToken ct)
    {
        File.Delete(Path.Combine(directory, fileName));
        return Task.CompletedTask;
    }
}
