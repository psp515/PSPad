using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Reading;
using PSPad.Module.Money.Balance;

namespace PSPad.App.State.Investments;

public sealed record LinkChoice(
    InvestmentAccount Account, AccountValueOnDay Value, decimal RateToPln, DateOnly RateDate, Holding? Existing);
