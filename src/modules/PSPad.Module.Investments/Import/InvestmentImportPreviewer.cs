using System.Globalization;
using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Portfolio;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Investments.Import;

public static class InvestmentImportPreviewer
{
    static readonly Guid NewAccountStandIn = Guid.Parse("6e657761-6363-6f75-6e74-000000000001");

    public static IReadOnlySet<ImportRateKey> RatesNeeded(XtbFile file, string accountCurrency, TimeZoneInfo zone)
    {
        var currency = accountCurrency.Trim().ToUpperInvariant();
        return currency == Currencies.Pln
            ? new HashSet<ImportRateKey>()
            : file.Rows.Where(row => row.Errors.Count == 0 && row.At is not null)
                .Select(row => new ImportRateKey(currency, ImportCommands.DayOf(row.At!.Value, zone)))
                .ToHashSet();
    }

    public static InvestmentImportPreview Preview(
        XtbFile file, InvestmentAccount? target, IReadOnlyList<InvestmentAccount> accounts,
        IReadOnlyList<InvestmentOperation> operations, InvestmentPortfolio? portfolio, string accountCurrency,
        TimeZoneInfo zone, IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)> rates,
        IReadOnlySet<int> untickedRows, IReadOnlySet<int> tickedDeletedRows)
    {
        var account = target ?? accounts.FirstOrDefault(candidate => candidate.BrokerAccountNumber == file.AccountNumber);
        var accountLimitReached = account is null && (portfolio?.AccountIds.Count ?? 0) >= InvestmentPortfolio.MaxAccounts;
        var newAccount = account is null && !accountLimitReached ? Proposal(file) : null;
        var fileError = FileErrorOf(file, target, account, accounts);

        ImportProbe probe;
        try
        {
            probe = ImportProbe.For(account?.Id ?? NewAccountStandIn, account?.Currency ?? accountCurrency, zone);
        }
        catch (DomainRejectedException rejected)
        {
            return new InvestmentImportPreview(account, newAccount, fileError ?? rejected.Message,
                file.Rows.Where(row => row.Errors.Count > 0).Select(row => Rejected(row, row.Errors, zone)).ToList(),
                [], null, accountLimitReached ? AccountLimit : null) { AccountNumber = file.AccountNumber };
        }

        var existing = account is null
            ? new Dictionary<Guid, InvestmentOperation>()
            : operations.Where(operation => operation.AccountId == account.Id).ToDictionary(operation => operation.Id);
        var rows = RowsOf(file, account, existing, probe, zone, rates, untickedRows, tickedDeletedRows);
        var newTickers = NewTickersOf(rows, portfolio);
        var limitError = accountLimitReached
            ? AccountLimit
            : OperationLimitOf(account, rows) ?? InstrumentLimitOf(portfolio, newTickers);
        var differences = file.OpenPositions is { } positions
            ? OpenPositionCheck.Differences(positions, existing.Values.Concat(rows
                .Where(row => row.Ticked)
                .Select(row => probe.Transient(row.Row))))
            : null;

        return new InvestmentImportPreview(account, newAccount, fileError, rows, newTickers, differences, limitError)
        {
            AccountNumber = file.AccountNumber
        };
    }

    static string AccountLimit => $"You can have at most {InvestmentPortfolio.MaxAccounts} investment accounts.";

    static NewAccountProposal Proposal(XtbFile file)
    {
        var name = $"{file.Product ?? "XTB"} {file.AccountNumber}".Trim();
        return new NewAccountProposal(name.Length > InvestmentAccount.MaxNameLength ? name[..InvestmentAccount.MaxNameLength] : name,
            file.AccountNumber);
    }

    static string? FileErrorOf(
        XtbFile file, InvestmentAccount? target, InvestmentAccount? account, IReadOnlyList<InvestmentAccount> accounts)
    {
        if (target?.BrokerAccountNumber is { } number && number != file.AccountNumber)
        {
            return $"This file is from account {file.AccountNumber}.";
        }

        if (target is { BrokerAccountNumber: null }
            && accounts.FirstOrDefault(other => other.Id != target.Id && other.BrokerAccountNumber == file.AccountNumber) is { } owner)
        {
            return $"This file belongs to account {owner.Name}.";
        }

        if (file.AccountNumber.Length > InvestmentAccount.MaxBrokerAccountNumberLength)
        {
            return $"A broker account number is at most {InvestmentAccount.MaxBrokerAccountNumberLength} characters.";
        }

        return account?.IsArchived == true ? "This account is archived." : null;
    }

    static List<ImportRowPreview> RowsOf(
        XtbFile file, InvestmentAccount? account, IReadOnlyDictionary<Guid, InvestmentOperation> existing,
        ImportProbe probe, TimeZoneInfo zone, IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)> rates,
        IReadOnlySet<int> untickedRows, IReadOnlySet<int> tickedDeletedRows)
    {
        var firstRowOfId = new Dictionary<string, int>(StringComparer.Ordinal);
        var rows = new List<ImportRowPreview>(file.Rows.Count);
        foreach (var row in file.Rows)
        {
            if (row.Errors.Count > 0)
            {
                rows.Add(Rejected(row, row.Errors, zone));
                continue;
            }

            if (!firstRowOfId.TryAdd(row.Id!, row.SheetRow))
            {
                rows.Add(Rejected(row, [$"The ID {row.Id} is already on row {firstRowOfId[row.Id!]}."], zone));
                continue;
            }

            if (probe.RejectionOf(row) is { } rejection)
            {
                rows.Add(Rejected(row, [rejection], zone));
                continue;
            }

            var day = ImportCommands.DayOf(row.At!.Value, zone);
            var id = account is null ? (Guid?)null : InvestmentOperation.IdFor(account.Id, ImportCommands.XtbId(row));
            rows.Add(id is { } known && existing.TryGetValue(known, out var operation)
                ? operation.Deleted
                    ? new ImportRowPreview(row, ImportRowStatus.DeletedEarlier, id, day, [], tickedDeletedRows.Contains(row.SheetRow))
                    : new ImportRowPreview(row, ImportRowStatus.AlreadyImported, id, day, [], false)
                : probe.Currency != Currencies.Pln && !rates.ContainsKey(new ImportRateKey(probe.Currency, day))
                    ? new ImportRowPreview(row, ImportRowStatus.WaitingForRate, id, day, [], false)
                    : new ImportRowPreview(row, ImportRowStatus.ToImport, id, day, [], !untickedRows.Contains(row.SheetRow)));
        }

        return rows;
    }

    static ImportRowPreview Rejected(XtbRow row, IReadOnlyList<string> errors, TimeZoneInfo zone) =>
        new(row, ImportRowStatus.Error, null, row.At is { } at ? ImportCommands.DayOf(at, zone) : null, errors, false);

    static List<string> NewTickersOf(IEnumerable<ImportRowPreview> rows, InvestmentPortfolio? portfolio)
    {
        var known = portfolio?.Tickers.ToHashSet(StringComparer.Ordinal) ?? [];
        return rows.Where(row => row is { Status: ImportRowStatus.ToImport, Ticked: true })
            .Select(row => ImportCommands.TickerOf(row.Row))
            .OfType<string>()
            .Where(ticker => !known.Contains(ticker))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    static string? OperationLimitOf(InvestmentAccount? account, IEnumerable<ImportRowPreview> rows)
    {
        var adding = rows.Count(row => row is { Status: ImportRowStatus.ToImport, Ticked: true });
        var excess = (account?.OperationCount ?? 0) + adding - InvestmentAccount.MaxOperations;
        return excess > 0
            ? $"This import needs {excess.ToString("N0", CultureInfo.InvariantCulture)} more operations than the account can take."
            : null;
    }

    static string? InstrumentLimitOf(InvestmentPortfolio? portfolio, IReadOnlyCollection<string> newTickers)
    {
        var excess = (portfolio?.Tickers.Count ?? 0) + newTickers.Count - InvestmentPortfolio.MaxInstruments;
        return excess > 0
            ? $"This import needs {excess.ToString("N0", CultureInfo.InvariantCulture)} more instruments than you can have."
            : null;
    }
}
