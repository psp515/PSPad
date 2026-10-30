using System.Text.Json.Serialization;

namespace PSPad.Module.Investments.Reading;

public sealed record PriceHistory(string Symbol, string Currency, IReadOnlyList<DailyClose> Closes)
{
    static readonly IReadOnlyDictionary<string, string> MinorUnits = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GBp"] = "GBP",
        ["GBX"] = "GBP",
        ["ILA"] = "ILS",
        ["ZAc"] = "ZAR"
    };

    readonly IReadOnlyList<DailyClose> closes = ByDate(Closes);

    public IReadOnlyList<DailyClose> Closes
    {
        get => closes;
        init => closes = ByDate(value);
    }

    [JsonIgnore]
    public decimal UnitFactor => MinorUnits.ContainsKey(Currency) ? 0.01m : 1m;

    [JsonIgnore]
    public string MajorCurrency => MinorUnits.TryGetValue(Currency, out var major) ? major : Currency;

    public DailyClose? On(DateOnly day) => Dated.LastOnOrBefore(closes, day, close => close.Date);

    static IReadOnlyList<DailyClose> ByDate(IReadOnlyList<DailyClose> closes) =>
        closes.Zip(closes.Skip(1)).All(pair => pair.First.Date <= pair.Second.Date)
            ? closes
            : closes.OrderBy(close => close.Date).ToList();
}
