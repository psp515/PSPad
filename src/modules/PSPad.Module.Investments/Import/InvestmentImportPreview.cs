using PSPad.Module.Investments.Accounts;

namespace PSPad.Module.Investments.Import;

public sealed record InvestmentImportPreview(
    IReadOnlyList<ImportAccountPreview> Accounts, IReadOnlyList<string> NewTickers,
    IReadOnlyList<OpenPositionDifference>? OpenPositionDifferences, string? FileLimitError)
{
    public string AccountNumber => Accounts[0].AccountNumber;

    public InvestmentAccount? Account => Accounts[0].Account;

    public NewAccountProposal? NewAccount => Accounts[0].NewAccount;

    public IEnumerable<NewAccountProposal> NewAccounts => Accounts.Select(section => section.NewAccount).OfType<NewAccountProposal>();

    public string? FileError => Accounts.Select(section => section.FileError).FirstOrDefault(error => error is not null);

    public string? LimitError =>
        FileLimitError ?? Accounts.Select(section => section.LimitError).FirstOrDefault(error => error is not null);

    public IReadOnlyList<ImportRowPreview> Rows { get; } =
        [.. Accounts.SelectMany(section => section.Rows).OrderBy(row => row.Row.SheetRow)];

    public IEnumerable<ImportRowPreview> Errors => Rows.Where(row => row.Status == ImportRowStatus.Error);

    public bool CanImport =>
        FileError is null && LimitError is null
        && Accounts.All(section => section.HasTarget || section.Rows.Count == 0)
        && Rows.All(row => row.Status != ImportRowStatus.WaitingForRate) && Rows.Any(row => row.Ticked);
}
