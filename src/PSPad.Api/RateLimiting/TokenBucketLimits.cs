namespace PSPad.Api.RateLimiting;

public sealed class TokenBucketLimits
{
    public int TokenLimit { get; set; }

    public int TokensPerPeriod { get; set; }

    public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(1);
}
