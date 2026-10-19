namespace PSPad.App.Live;

public interface ILiveStream
{
    Task<Stream> OpenAsync(CancellationToken ct);
}
