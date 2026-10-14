using PSPad.App.Live;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Live;

[UnitTest]
public class SseReaderTests
{
    static async Task<List<string>> ReadAsync(params string[] chunks)
    {
        var frames = new List<string>();
        await foreach (var frame in SseReader.FramesAsync(new ChunkedStream(chunks), CancellationToken.None))
        {
            frames.Add(frame);
        }

        return frames;
    }

    [Fact]
    public async Task EachFrameYieldsItsEventName() =>
        Assert.Equal(["ready", "sync"], await ReadAsync("event: ready\ndata: ready\n\nevent: sync\ndata: sync\n\n"));

    [Fact]
    public async Task ACommentIsAHeartbeat() =>
        Assert.Equal([SseReader.Heartbeat], await ReadAsync(": ping\n\n"));

    [Fact]
    public async Task AFrameSplitAcrossReadsStillArrivesWhole() =>
        Assert.Equal(["sync"], await ReadAsync("eve", "nt: sy", "nc\ndata: sync\n", "\n"));

    [Fact]
    public async Task CarriageReturnLineEndingsAreAccepted() =>
        Assert.Equal(["ready"], await ReadAsync("event: ready\r\ndata: ready\r\n\r\n"));

    [Fact]
    public async Task AFrameWithoutAnEventNameIsSkipped() =>
        Assert.Equal(["sync"], await ReadAsync("data: hello\n\nevent: sync\ndata: sync\n\n"));

    [Fact]
    public async Task AFrameCutOffBeforeItsBlankLineIsNotYielded() =>
        Assert.Empty(await ReadAsync("event: sync\ndata: sync\n"));
}
