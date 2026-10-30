using PSPad.Module.Investments.Accounts;

namespace PSPad.Module.Investments.Import;

public sealed record InvestmentImportPreview(
    InvestmentAccount? Account, NewAccountProposal? NewAccount, string? FileError, IReadOnlyList<ImportRowPreview> Rows,
    IReadOnlyList<string> NewTickers, IReadOnlyList<OpenPositionDifference>? OpenPositionDifferences, string? LimitError)
{
    public string AccountNumber { get; init; } = "";

    public IEnumerable<ImportRowPreview> Errors => Rows.Where(row => row.Status == ImportRowStatus.Error);

    public bool CanImport =>
        FileError is null && LimitError is null && (Account is not null || NewAccount is not null)
        && Rows.All(row => row.Status != ImportRowStatus.WaitingForRate) && Rows.Any(row => row.Ticked);
}
