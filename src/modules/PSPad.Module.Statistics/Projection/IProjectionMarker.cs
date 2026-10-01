namespace PSPad.Module.Statistics;

public interface IProjectionMarker
{
    Task<long> ReadAsync(CancellationToken ct);

    Task WriteAsync(long seq, CancellationToken ct);

    // true when the stored version was older (or absent): the marker is reset to 0 and the new version recorded.
    Task<bool> AdoptVersionAsync(int version, CancellationToken ct);
}
