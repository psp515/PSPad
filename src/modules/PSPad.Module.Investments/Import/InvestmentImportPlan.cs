using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Investments.Import;

public static class InvestmentImportPlan
{
    public static IReadOnlyList<ICommand> Commands(
        InvestmentImportPreview preview, Guid userId, IReadOnlyDictionary<string, NewAccountChoice> newAccounts,
        IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)> rates, Func<Guid> newId)
    {
        if (!preview.CanImport)
        {
            return [];
        }

        var setup = new List<ICommand>();
        var restores = new List<ICommand>();
        var importing = new List<(ImportRowPreview Row, Guid AccountId, string Currency)>();

        foreach (var section in preview.Accounts.Where(section => section.Rows.Any(row => row.Ticked)))
        {
            var accountId = section.Account?.Id ?? AccountIdOf(section, setup, userId, newAccounts, newId);
            if (section.Account is { BrokerAccountNumber: null })
            {
                setup.Add(new SetBrokerAccountNumber(newId(), userId, accountId, section.AccountNumber));
            }

            restores.AddRange(section.Rows.Where(row => row is { Ticked: true, Status: ImportRowStatus.DeletedEarlier })
                .Select(row => new RestoreInvestmentOperation(newId(), userId, row.OperationId!.Value)));
            importing.AddRange(section.Rows.Where(row => row is { Ticked: true, Status: ImportRowStatus.ToImport })
                .Select(row => (row, accountId, section.Currency)));
        }

        var ordered = importing.OrderBy(item => item.Row.Row.At).ThenBy(item => item.Row.Row.SheetRow).ToList();
        var commands = new List<ICommand>(setup);
        commands.AddRange(preview.NewTickers.Select(ticker => new EnsureInstrument(newId(), userId, ticker,
            NameOf(ordered.First(item => ImportCommands.TickerOf(item.Row.Row) == ticker).Row.Row))));
        commands.AddRange(restores);
        commands.AddRange(ordered.Select(item => ImportCommands.Record(item.Row.Row, newId(), userId, item.AccountId,
            item.Currency, item.Row.Day!.Value, RateOf(rates, item.Currency, item.Row.Day!.Value))));
        return commands;
    }

    static Guid AccountIdOf(
        ImportAccountPreview section, List<ICommand> setup, Guid userId,
        IReadOnlyDictionary<string, NewAccountChoice> newAccounts, Func<Guid> newId)
    {
        var proposal = section.NewAccount!;
        var choice = newAccounts.GetValueOrDefault(section.AccountNumber);
        var accountId = choice?.AccountId ?? newId();
        var name = string.IsNullOrWhiteSpace(choice?.Name) ? proposal.Name : choice.Name.Trim();
        setup.Add(new CreateInvestmentAccount(newId(), userId, accountId, name, Currencies.Require(section.Currency),
            Broker.Xtb, proposal.AccountNumber));
        return accountId;
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
