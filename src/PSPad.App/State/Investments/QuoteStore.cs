using Microsoft.JSInterop;
using PSPad.Contracts;

namespace PSPad.App.State.Investments;

public sealed class QuoteStore(IJSRuntime js) : IQuoteStore, IAsyncDisposable
{
    IJSObjectReference? _module;

    public Task SaveQuoteAsync(QuoteSeriesView series, DateOnly from) =>
        PutAsync(new QuoteRow(series.Symbol, series, null, from));

    public async Task<StoredQuote?> QuoteAsync(string symbol) =>
        await GetAsync(symbol) is { Series: { } series, From: { } from } ? new StoredQuote(series, from) : null;

    public Task SaveRatesAsync(string currency, IReadOnlyList<NbpRateView> rates, DateOnly from) =>
        PutAsync(new QuoteRow(RatesKey(currency), null, rates.ToArray(), from));

    public async Task<StoredRates> RatesAsync(string currency) =>
        await GetAsync(RatesKey(currency)) is { } row ? new StoredRates(row.Rates ?? [], row.From) : new StoredRates([], null);

    public async Task ClearAsync()
    {
        try
        {
            var module = await ModuleAsync();
            await module.InvokeVoidAsync("clearQuotes");
        }
        catch (JSException)
        {
        }
    }

    static string RatesKey(string currency) => $"fx:{currency.Trim().ToUpperInvariant()}";

    async Task PutAsync(QuoteRow row)
    {
        try
        {
            var module = await ModuleAsync();
            await module.InvokeVoidAsync("putQuote", row);
        }
        catch (JSException)
        {
        }
    }

    async Task<QuoteRow?> GetAsync(string key)
    {
        try
        {
            var module = await ModuleAsync();
            return await module.InvokeAsync<QuoteRow?>("getQuote", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/replica.js");

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    sealed record QuoteRow(string Key, QuoteSeriesView? Series, NbpRateView[]? Rates, DateOnly? From);
}
