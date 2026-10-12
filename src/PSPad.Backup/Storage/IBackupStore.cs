namespace PSPad.Backup.Storage;

public interface IBackupStore
{
    string Name { get; }

    Task<IReadOnlyList<string>> ListAsync(CancellationToken ct);

    Task PutAsync(string source, string fileName, CancellationToken ct);

    Task FetchAsync(string fileName, string destination, CancellationToken ct);

    Task DeleteAsync(string fileName, CancellationToken ct);
}
