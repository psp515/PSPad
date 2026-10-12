namespace PSPad.Api.RateLimiting;

public sealed class UserRateLimits
{
    public const string Section = "RateLimits";

    public TokenBucketLimits Api { get; set; } = new() { TokenLimit = 120, TokensPerPeriod = 12 };

    public TokenBucketLimits Commands { get; set; } = new() { TokenLimit = 30, TokensPerPeriod = 3 };

    public TokenBucketLimits Sync { get; set; } = new() { TokenLimit = 30, TokensPerPeriod = 3 };

    public TokenBucketLimits Statistics { get; set; } = new() { TokenLimit = 60, TokensPerPeriod = 6 };
}
