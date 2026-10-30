using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Investments.Import;

public static class InvestmentImportPlan
{
    public static IReadOnlyList<ICommand> Commands(
        InvestmentImportPreview preview, Guid userId, Guid accountId, string accountName, string accountCurrency,
        IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)> rates, Func<Guid> newId)
    {
        if (!preview.CanImport)
        {
            return [];
        }

        var id = preview.Account?.Id ?? accountId;
        var currency = preview.Account?.Currency ?? Currencies.Require(accountCurrency);
        var ticked = preview.Rows.Where(row => row.Ticked).ToList();
        var importing = ticked.Where(row => row.Status == ImportRowStatus.ToImport)
            .OrderBy(row => row.Row.At).ThenBy(row => row.Row.SheetRow).ToList();
        var commands = new List<ICommand>();

        if (preview.NewAccount is { } proposal)
        {
            commands.Add(new CreateInvestmentAccount(newId(), userId, id, accountName, currency, Broker.Xtb,
                proposal.AccountNumber));
        }
        else if (preview.Account?.BrokerAccountNumber is null)
        {
            commands.Add(new SetBrokerAccountNumber(newId(), userId, id, preview.AccountNumber));
        }

        commands.AddRange(preview.NewTickers.Select(ticker => new EnsureInstrument(newId(), userId, ticker,
            NameOf(importing.First(row => ImportCommands.TickerOf(row.Row) == ticker).Row))));
        commands.AddRange(ticked.Where(row => row.Status == ImportRowStatus.DeletedEarlier)
            .Select(row => new RestoreInvestmentOperation(newId(), userId, row.OperationId!.Value)));
        commands.AddRange(importing.Select(row => ImportCommands.Record(row.Row, newId(), userId, id, currency, row.Day!.Value,
            RateOf(rates, currency, row.Day!.Value))));
        return commands;
    }

    static string NameOf(XtbRow row)
    {
        var name = row.Instrument?.Trim() ?? "";
        return name.Length > Instrument.MaxNameLength ? name[..Instrument.MaxNameLength].TrimEnd() : name;
    }

    static (decimal Rate, DateOnly RateDate) RateOf(
        IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)> rates, string currency, DateOnly day) =>
        currency == Currencies.Pln
            ? (1m, day)
            : rates.TryGetValue(new ImportRateKey(currency, day), out var rate)
                ? rate
                : throw new InvalidOperationException($"No {currency} rate for {day:yyyy-MM-dd}.");
}
