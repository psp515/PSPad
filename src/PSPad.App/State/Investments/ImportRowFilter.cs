using PSPad.Module.Investments.Import;

namespace PSPad.App.State.Investments;

public static class ImportRowFilter
{
    public static IEnumerable<ImportRowPreview> Visible(IEnumerable<ImportRowPreview> rows, bool errorsOnly) =>
        errorsOnly ? rows.Where(row => row.Status == ImportRowStatus.Error) : rows;
}
