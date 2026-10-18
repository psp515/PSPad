using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.Module.Money.Import;

namespace PSPad.App.State;

public static class ImportText
{
    public static string Amount(decimal amount, string currency) =>
        $"{amount.ToString("0.00", CultureInfo.CurrentCulture)} {currency}";

    public static string RateSource(decimal? fileRate, PSPad.Module.Money.Money? money, IStringLocalizer<BudgetsStrings> text) =>
        money is not { Currency: not "PLN" } known
            ? ""
            : fileRate is null
                ? text["ImportRateNbp", known.RateToPln.ToString(CultureInfo.CurrentCulture),
                    known.RateDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)]
                : text["ImportRateFile", known.RateToPln.ToString(CultureInfo.CurrentCulture)];

    public static string StatusClass(ImportStatus status) => status == ImportStatus.Error ? "mud-error-text" : "";

    public static string StatusText(ImportStatus status, IStringLocalizer<BudgetsStrings> text) => status switch
    {
        ImportStatus.ToImport => text["ImportToImport"],
        ImportStatus.AlreadyInBudget => text["ImportAlreadyInBudget"],
        ImportStatus.Unchanged => text["ImportUnchanged"],
        ImportStatus.WaitingForRate => text["ImportFetchingRate"],
        _ => text["ImportError"]
    };
}
