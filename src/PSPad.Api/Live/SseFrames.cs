namespace PSPad.Api.Live;

public static class SseFrames
{
    public static readonly byte[] Ready = "event: ready\ndata: ready\n\n"u8.ToArray();

    public static readonly byte[] Sync = "event: sync\ndata: sync\n\n"u8.ToArray();

    public static readonly byte[] Heartbeat = ": ping\n\n"u8.ToArray();
}
