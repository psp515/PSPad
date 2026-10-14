namespace PSPad.Api.Live;

public sealed class LiveOptions
{
    public const string Section = "Live";

    public TimeSpan Heartbeat { get; set; } = TimeSpan.FromSeconds(25);

    public TimeSpan MaxStream { get; set; } = TimeSpan.FromMinutes(15);

    public int MaxStreamsPerUser { get; set; } = 10;
}
