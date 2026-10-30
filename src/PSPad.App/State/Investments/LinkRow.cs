using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Reading;
using PSPad.Module.Money.Balance;

namespace PSPad.App.State.Investments;

public sealed class LinkRow(InvestmentAccount account, Holding? existing)
{
    public InvestmentAccount Account { get; } = account;

    public Holding? Existing { get; } = existing;

    public bool Ticked { get; set; } = true;

    public bool Loading { get; set; } = true;

    public AccountValueOnDay? Value { get; set; }

    public decimal? Rate { get; set; }

    public DateOnly RateDate { get; set; }

    public PriceNote Note { get; set; } = PriceNote.None;
}
