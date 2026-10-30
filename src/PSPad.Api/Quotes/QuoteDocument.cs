using MongoDB.Bson.Serialization.Attributes;

namespace PSPad.Api.Quotes;

public sealed class QuoteDocument
{
    [BsonId]
    public string Id { get; set; } = "";

    public string Currency { get; set; } = "";

    public DateOnly From { get; set; }

    public List<QuoteCloseDocument> Closes { get; set; } = [];

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LatestAt { get; set; }

    public decimal? LatestPrice { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime FetchedAt { get; set; }
}
