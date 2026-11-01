using PSPad.Module.Investments.Accounts;

namespace PSPad.Module.Investments.Import;

public sealed record ImportAccountPreview(
    string AccountNumber, string? Product, InvestmentAccount? Account, NewAccountProposal? NewAccount, string Currency,
    string? FileError, IReadOnlyList<ImportRowPreview> Rows, string? LimitError)
{
    public bool HasTarget => Account is not null || NewAccount is not null;
}
