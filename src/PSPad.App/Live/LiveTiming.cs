namespace PSPad.App.Live;

public sealed record LiveTiming(TimeSpan Silence, Func<double> Jitter, Func<TimeSpan, CancellationToken, Task> Delay)
{
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    public static LiveTiming Default { get; } = new(
        TimeSpan.FromSeconds(60),
        () => 0.5 + Random.Shared.NextDouble() / 2,
        Task.Delay);

    public TimeSpan BackoffAfter(int failures) =>
        TimeSpan.FromSeconds(Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, failures - 1))) * Jitter();
}
