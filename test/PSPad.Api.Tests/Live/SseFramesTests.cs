using System.Text;
using PSPad.Api.Live;
using PSPad.App.Live;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Live;

[UnitTest]
public class SseFramesTests
{
    [Fact]
    public void TheFramesAreExactlyTheWireFormat()
    {
        Assert.Equal("event: ready\ndata: ready\n\n", Encoding.UTF8.GetString(SseFrames.Ready));
        Assert.Equal("event: sync\ndata: sync\n\n", Encoding.UTF8.GetString(SseFrames.Sync));
        Assert.Equal(": ping\n\n", Encoding.UTF8.GetString(SseFrames.Heartbeat));
    }

    [Fact]
    public async Task TheClientReaderUnderstandsEveryFrame()
    {
        using var stream = new MemoryStream([.. SseFrames.Ready, .. SseFrames.Sync, .. SseFrames.Heartbeat]);
        var frames = new List<string>();
        await foreach (var frame in SseReader.FramesAsync(stream, CancellationToken.None))
        {
            frames.Add(frame);
        }

        Assert.Equal(["ready", "sync", SseReader.Heartbeat], frames);
    }
}
