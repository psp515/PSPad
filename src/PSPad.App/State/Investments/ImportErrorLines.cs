using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.Module.Investments.Import;

namespace PSPad.App.State.Investments;

public static class ImportErrorLines
{
    public static IReadOnlyList<ImportErrorLine> Of(IEnumerable<ImportRowPreview> rows, IStringLocalizer<InvestmentImportStrings> text) =>
        [.. rows.Where(row => row.Status == ImportRowStatus.Error).Select(row => new ImportErrorLine(
            text["RowNumber", row.Row.SheetRow].Value, row.Row.Type,
            row.Row.At is { } at ? at.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "",
            row.Row.Ticker ?? "", row.Row.Amount?.ToString("0.########", CultureInfo.InvariantCulture) ?? "",
            row.Row.Id ?? "", row.Row.Comment ?? "", row.Errors))];
}
