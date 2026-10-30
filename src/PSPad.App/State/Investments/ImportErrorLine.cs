namespace PSPad.App.State.Investments;

public sealed record ImportErrorLine(
    string RowLabel, string Type, string Time, string Ticker, string Amount, string Id, string Comment,
    IReadOnlyList<string> Reasons);
