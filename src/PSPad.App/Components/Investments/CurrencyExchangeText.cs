using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Localization;
using PSPad.App.Resources;
using PSPad.App.State;
using PSPad.App.State.Investments;
using PSPad.Module.Investments.Reading;

namespace PSPad.App.Components.Investments;

public sealed record CurrencyExchangeText(string? Bought, string? Sold, string? Held, string? Result, string Span)
{
    public static CurrencyExchangeText For(CurrencyExchange exchange, IStringLocalizer<InvestmentsStrings> text) =>
        new(Side(exchange.BoughtPricedUnits > 0
                    ? text["ExchangeBought", MoneyText.Amount(exchange.BoughtPricedUnits), MoneyText.Pln(exchange.BoughtPln),
                        Rate(exchange.BoughtRate)].Value
                    : null,
                exchange.BoughtUnits - exchange.BoughtPricedUnits is > 0 and var boughtElsewhere
                    ? text["ExchangeBoughtOther", MoneyText.Amount(boughtElsewhere)].Value
                    : null),
            Side(exchange.SoldPricedUnits > 0
                    ? text["ExchangeSold", MoneyText.Amount(exchange.SoldPricedUnits), MoneyText.Pln(exchange.SoldPln),
                        Rate(exchange.SoldRate)].Value
                    : null,
                exchange.SoldUnits - exchange.SoldPricedUnits is > 0 and var soldElsewhere
                    ? text["ExchangeSoldOther", MoneyText.Amount(soldElsewhere)].Value
                    : null),
            exchange.HeldUnits is { } held ? text["ExchangeHeld", MoneyText.Amount(held), Rate(exchange.TodayRate)].Value : null,
            exchange.CurrencyResult is { } result ? MoneyText.SignedPln(result) : null,
            text["ExchangeSpan", text.Plural(exchange.Count, "ConversionsCount"), InvestmentText.Day(exchange.First),
                InvestmentText.Day(exchange.Last)].Value);

    static string? Side(string? priced, string? elsewhere) =>
        priced is null ? elsewhere : elsewhere is null ? priced : $"{priced} · {elsewhere}";

    public static string Rate(decimal? rate) => rate is { } value ? value.ToString("N4", CultureInfo.CurrentCulture) : "—";
}
