namespace PSPad.Module.Statistics;

public interface IProjectionMarker
{
    Task<long> ReadAsync(CancellationToken ct);

    Task WriteAsync(long seq, CancellationToken ct);
}
