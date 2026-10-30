using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Import;

internal sealed class ImportProbe
{
    static readonly Guid ProbeUser = Guid.Parse("70726f62-6500-0000-0000-000000000001");

    readonly InvestmentAccount account = new();
    readonly TimeZoneInfo zone;

    ImportProbe(TimeZoneInfo zone) => this.zone = zone;

    public string Currency => account.Currency;

    public static ImportProbe For(Guid accountId, string currency, TimeZoneInfo zone)
    {
        var probe = new ImportProbe(zone);
        probe.account.ApplyAll(InvestmentAccount.Decide(null,
            new CreateInvestmentAccount(Guid.Empty, ProbeUser, accountId, "Import", currency, Broker.Xtb, null),
            DateTimeOffset.UnixEpoch));
        return probe;
    }

    public string? RejectionOf(XtbRow row)
    {
        try
        {
            InvestmentOperation.Decide(null, account, RecordOf(row), DateTimeOffset.UnixEpoch);
            return null;
        }
        catch (DomainRejectedException rejected)
        {
            return rejected.Message;
        }
    }

    public InvestmentOperation Transient(XtbRow row)
    {
        var operation = new InvestmentOperation();
        operation.ApplyAll(InvestmentOperation.Decide(null, account, RecordOf(row), DateTimeOffset.UnixEpoch));
        return operation;
    }

    RecordInvestmentOperation RecordOf(XtbRow row)
    {
        var day = ImportCommands.DayOf(row.At!.Value, zone);
        return ImportCommands.Record(row, Guid.Empty, ProbeUser, account.Id, account.Currency, day, (1m, day));
    }
}
