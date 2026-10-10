namespace PSPad.Api.Rates;

public static class NbpCacheLifetime
{
    public static TimeSpan For(DateOnly date, DateOnly today) =>
        date < today ? TimeSpan.FromHours(24) : TimeSpan.FromHours(1);
}
