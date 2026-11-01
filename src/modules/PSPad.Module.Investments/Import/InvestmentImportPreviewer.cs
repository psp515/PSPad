using System.Globalization;
using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Portfolio;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Investments.Import;

public static class InvestmentImportPreviewer
{
    public static IReadOnlySet<ImportRateKey> RatesNeeded(InvestmentImportPreview preview) =>
        preview.Accounts.Where(section => section.Currency != Currencies.Pln)
            .SelectMany(section => section.Rows
                .Where(row => row.Status != ImportRowStatus.Error && row.Day is not null)
                .Select(row => new ImportRateKey(section.Currency, row.Day!.Value)))
            .ToHashSet();

    public static InvestmentImportPreview Preview(
        XtbFile file, InvestmentAccount? target, IReadOnlyList<InvestmentAccount> accounts,
        IReadOnlyList<InvestmentOperation> operations, InvestmentPortfolio? portfolio, string accountCurrency,
        TimeZoneInfo zone, IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)> rates,
        IReadOnlySet<int> untickedRows, IReadOnlySet<int> tickedDeletedRows)
    {
        var numbers = file.AccountNumbers;
        var targetNumber = target?.BrokerAccountNumber is { } held && numbers.Contains(held) ? held : file.AccountNumber;
        var rowsByNumber = file.Rows.GroupBy(file.AccountNumberOf, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var openSlots = InvestmentPortfolio.MaxAccounts - (portfolio?.AccountIds.Count ?? 0);
        var accountLimitReached = false;
        var firstRowOfId = new Dictionary<string, int>(StringComparer.Ordinal);
        var sections = new List<ImportAccountPreview>();
        var transient = new List<InvestmentOperation>();
        var existing = new List<InvestmentOperation>();
        string? mainCurrency = null;

        for (var index = 0; index < numbers.Count; index++)
        {
            var number = numbers[index];
            var rows = rowsByNumber.GetValueOrDefault(number) ?? [];
            var account = target is not null && number == targetNumber
                ? target
                : accounts.FirstOrDefault(candidate => candidate.BrokerAccountNumber == number);
            NewAccountProposal? proposal = null;
            if (account is null && rows.Count > 0)
            {
                if (openSlots > 0)
                {
                    openSlots--;
                    proposal = Proposal(file, number);
                }
                else
                {
                    accountLimitReached = true;
                }
            }

            var currency = account?.Currency ?? mainCurrency ?? accountCurrency;
            mainCurrency ??= currency;
            var fileError = FileErrorOf(file, number, target, account, accounts);
            var section = SectionOf(file, number, rows, account, proposal, currency, fileError, index, operations, zone,
                rates, untickedRows, tickedDeletedRows, firstRowOfId, existing, transient);
            sections.Add(section);
        }

        var newTickers = NewTickersOf(sections.SelectMany(section => section.Rows), portfolio);
        var fileLimit = accountLimitReached ? AccountLimit : InstrumentLimitOf(portfolio, newTickers);
        var differences = file.OpenPositions is { } positions
            ? OpenPositionCheck.Differences(positions, existing.Concat(transient))
            : null;

        return new InvestmentImportPreview(sections, newTickers, differences, fileLimit);
    }

    static ImportAccountPreview SectionOf(
        XtbFile file, string number, IReadOnlyList<XtbRow> rows, InvestmentAccount? account, NewAccountProposal? proposal,
        string currency, string? fileError, int index, IReadOnlyList<InvestmentOperation> operations, TimeZoneInfo zone,
        IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)> rates, IReadOnlySet<int> untickedRows,
        IReadOnlySet<int> tickedDeletedRows, Dictionary<string, int> firstRowOfId, List<InvestmentOperation> existing,
        List<InvestmentOperation> transient)
    {
        var product = file.ProductOf(number);
        ImportProbe probe;
        try
        {
            probe = ImportProbe.For(account?.Id ?? StandInFor(index), currency, zone);
        }
        catch (DomainRejectedException rejected)
        {
            return new ImportAccountPreview(number, product, account, proposal, currency.Trim().ToUpperInvariant(),
                fileError ?? rejected.Message,
                rows.Where(row => row.Errors.Count > 0).Select(row => Rejected(row, row.Errors, zone)).ToList(), null);
        }

        var known = account is null
            ? new Dictionary<Guid, InvestmentOperation>()
            : operations.Where(operation => operation.AccountId == account.Id).ToDictionary(operation => operation.Id);
        existing.AddRange(known.Values);
        var previews = RowsOf(rows, account, known, probe, zone, rates, untickedRows, tickedDeletedRows, firstRowOfId);
        transient.AddRange(previews.Where(row => row.Ticked).Select(row => probe.Transient(row.Row)));
        return new ImportAccountPreview(number, product, account, proposal, probe.Currency, fileError, previews,
            OperationLimitOf(account, previews));
    }

    static Guid StandInFor(int index) =>
        Guid.Parse($"6e657761-6363-6f75-6e74-{index + 1:x12}");

    static string AccountLimit => $"You can have at most {InvestmentPortfolio.MaxAccounts} investment accounts.";

    static NewAccountProposal Proposal(XtbFile file, string number)
    {
        var name = $"{file.ProductOf(number) ?? "XTB"} {number}".Trim();
        return new NewAccountProposal(name.Length > InvestmentAccount.MaxNameLength ? name[..InvestmentAccount.MaxNameLength] : name,
            number);
    }

    static string? FileErrorOf(
        XtbFile file, string number, InvestmentAccount? target, InvestmentAccount? account,
        IReadOnlyList<InvestmentAccount> accounts)
    {
        var isTarget = target is not null && ReferenceEquals(account, target);
        if (isTarget && target!.BrokerAccountNumber is { } held && held != number)
        {
            return $"This file is from account {file.AccountNumber}.";
        }

        if (isTarget && target!.BrokerAccountNumber is null
            && accounts.FirstOrDefault(other => other.Id != target.Id && other.BrokerAccountNumber == number) is { } owner)
        {
            return $"This file belongs to account {owner.Name}.";
        }

        if (number.Length > InvestmentAccount.MaxBrokerAccountNumberLength)
        {
            return $"A broker account number is at most {InvestmentAccount.MaxBrokerAccountNumberLength} characters.";
        }

        return account?.IsArchived == true ? "This account is archived." : null;
    }

    static List<ImportRowPreview> RowsOf(
        IReadOnlyList<XtbRow> rows, InvestmentAccount? account, IReadOnlyDictionary<Guid, InvestmentOperation> existing,
        ImportProbe probe, TimeZoneInfo zone, IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)> rates,
        IReadOnlySet<int> untickedRows, IReadOnlySet<int> tickedDeletedRows, Dictionary<string, int> firstRowOfId)
    {
        var previews = new List<ImportRowPreview>(rows.Count);
        foreach (var row in rows)
        {
            if (row.Errors.Count > 0)
            {
                previews.Add(Rejected(row, row.Errors, zone));
                continue;
            }

            if (!firstRowOfId.TryAdd(row.Id!, row.SheetRow))
            {
                previews.Add(Rejected(row, [$"The ID {row.Id} is already on row {firstRowOfId[row.Id!]}."], zone));
                continue;
            }

            if (probe.RejectionOf(row) is { } rejection)
            {
                previews.Add(Rejected(row, [rejection], zone));
                continue;
            }

            var day = ImportCommands.DayOf(row.At!.Value, zone);
            var id = account is null ? (Guid?)null : InvestmentOperation.IdFor(account.Id, ImportCommands.XtbId(row));
            previews.Add(id is { } known && existing.TryGetValue(known, out var operation)
                ? operation.Deleted
                    ? new ImportRowPreview(row, ImportRowStatus.DeletedEarlier, id, day, [], tickedDeletedRows.Contains(row.SheetRow))
                    : new ImportRowPreview(row, ImportRowStatus.AlreadyImported, id, day, [], false)
                : probe.Currency != Currencies.Pln && !rates.ContainsKey(new ImportRateKey(probe.Currency, day))
                    ? new ImportRowPreview(row, ImportRowStatus.WaitingForRate, id, day, [], false)
                    : new ImportRowPreview(row, ImportRowStatus.ToImport, id, day, [], !untickedRows.Contains(row.SheetRow)));
        }

        return previews;
    }

    static ImportRowPreview Rejected(XtbRow row, IReadOnlyList<string> errors, TimeZoneInfo zone) =>
        new(row, ImportRowStatus.Error, null, row.At is { } at ? ImportCommands.DayOf(at, zone) : null, errors, false);

    static List<string> NewTickersOf(IEnumerable<ImportRowPreview> rows, InvestmentPortfolio? portfolio)
    {
        var known = portfolio?.Tickers.ToHashSet(StringComparer.Ordinal) ?? [];
        return rows.Where(row => row is { Status: ImportRowStatus.ToImport, Ticked: true })
            .OrderBy(row => row.Row.SheetRow)
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
