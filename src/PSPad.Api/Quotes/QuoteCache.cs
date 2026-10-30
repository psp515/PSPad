using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Quotes;

public sealed class QuoteCache(MongoContext context, YahooQuoteClient yahoo, IClock clock)
{
    static readonly TimeSpan RefreshGrace = TimeSpan.FromMinutes(15);

    IMongoCollection<QuoteDocument> Collection => context.Collection<QuoteDocument>("quotes");

    public async Task<QuoteSeriesView?> SeriesAsync(string symbol, DateOnly from, bool refresh, CancellationToken ct)
    {
        var id = symbol.ToUpperInvariant();
        var cached = await Collection.Find(document => document.Id == id).FirstOrDefaultAsync(ct);
        var now = clock.UtcNow;

        if (cached is not null && IsFresh(cached, from, refresh, now))
        {
            return ViewOf(cached, from, stale: false);
        }

        var fetchFrom = cached is null ? from : DateOnly.FromDayNumber(Math.Min(from.DayNumber, cached.From.DayNumber));
        YahooChart? chart;

        try
        {
            chart = await yahoo.ChartAsync(id, fetchFrom, ct);
        }
        catch (QuoteUnavailableException) when (cached is not null)
        {
            return ViewOf(cached, from, stale: true);
        }

        if (chart is null)
        {
            return null;
        }

        var document = new QuoteDocument
        {
            Id = id,
            Currency = chart.Currency,
            From = fetchFrom,
            Closes = chart.Closes.Select(close => new QuoteCloseDocument { Date = close.Date, Close = close.Close }).ToList(),
            LatestAt = chart.Latest?.At.UtcDateTime,
            LatestPrice = chart.Latest?.Price,
            FetchedAt = now.UtcDateTime
        };

        await Collection.ReplaceOneAsync(
            stored => stored.Id == id, document, new ReplaceOptions { IsUpsert = true }, ct);

        return ViewOf(document, from, stale: false);
    }

    static bool IsFresh(QuoteDocument cached, DateOnly from, bool refresh, DateTimeOffset now)
    {
        var fetchedAt = new DateTimeOffset(DateTime.SpecifyKind(cached.FetchedAt, DateTimeKind.Utc));

        return from >= cached.From
            && DateOnly.FromDateTime(fetchedAt.UtcDateTime) == DateOnly.FromDateTime(now.UtcDateTime)
            && !(refresh && now - fetchedAt > RefreshGrace);
    }

    static QuoteSeriesView ViewOf(QuoteDocument document, DateOnly from, bool stale) =>
        new(document.Id,
            document.Currency,
            document.Closes.Where(close => close.Date >= from).OrderBy(close => close.Date)
                .Select(close => new QuoteCloseView(close.Date, close.Close)).ToList(),
            document.LatestAt is { } at && document.LatestPrice is { } price
                ? new QuoteLatestView(new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)), price)
                : null,
            new DateTimeOffset(DateTime.SpecifyKind(document.FetchedAt, DateTimeKind.Utc)),
            stale);
}
