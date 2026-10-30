using PSPad.Module.Investments.Reading;

namespace PSPad.Module.Investments.Tests.Reading;

internal static class Market
{
    public static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    public static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    public static readonly DateTimeOffset HalfPastMidnightInWarsaw = new(2026, 10, 31, 23, 30, 0, TimeSpan.Zero);
    public static readonly DateOnly LastOfOctober = new(2026, 10, 31);
    public static readonly DateOnly FirstOfNovember = new(2026, 11, 1);

    public static readonly FxBook PlnOnly = new(new Dictionary<string, IReadOnlyList<DailyRate>>());

    public static IReadOnlyDictionary<string, PriceHistory> Prices(
        params (string Ticker, string Currency, (DateOnly Day, decimal Close) Close)[] tickers) =>
        tickers.ToDictionary(price => price.Ticker,
            price => new PriceHistory(price.Ticker, price.Currency, [new DailyClose(price.Close.Day, price.Close.Close)]));

    public static IReadOnlyDictionary<string, PriceHistory> History(
        string ticker, string currency, params (DateOnly Day, decimal Close)[] closes) =>
        new Dictionary<string, PriceHistory>
        {
            [ticker] = new(ticker, currency, closes.Select(close => new DailyClose(close.Day, close.Close)).ToList())
        };

    public static FxBook Rates(params (string Currency, DateOnly Day, decimal Rate)[] rates) =>
        new(rates.GroupBy(rate => rate.Currency).ToDictionary(group => group.Key,
            group => (IReadOnlyList<DailyRate>)group.Select(rate => new DailyRate(rate.Day, rate.Rate)).ToList()));
}
