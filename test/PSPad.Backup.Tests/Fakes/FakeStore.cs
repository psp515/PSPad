using PSPad.Backup.Storage;

namespace PSPad.Backup.Tests.Fakes;

sealed class FakeStore(string name) : IBackupStore
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public bool FailUploads { get; init; }

    public string Name => name;

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(Files.Keys.ToArray());

    public async Task PutAsync(string source, string fileName, CancellationToken ct)
    {
        if (FailUploads)
        {
            throw new IOException($"{name} is unreachable");
        }

        Files[fileName] = await File.ReadAllBytesAsync(source, ct);
    }

    public Task FetchAsync(string fileName, string destination, CancellationToken ct) =>
        File.WriteAllBytesAsync(destination, Files[fileName], ct);

    public Task DeleteAsync(string fileName, CancellationToken ct)
    {
        Files.Remove(fileName);
        return Task.CompletedTask;
    }
}
