using Microsoft.Extensions.Localization;
using PSPad.App.Api;
using PSPad.App.Resources;

namespace PSPad.App.Components;

public static class SharingRejectionText
{
    public static string Of(this IStringLocalizer<SharingStrings> localizer, SharingRejection rejection) =>
        rejection.Failure switch
        {
            SharingFailure.ListGone => localizer["SharingListGone"],
            SharingFailure.BudgetGone => localizer["SharingBudgetGone"],
            SharingFailure.Offline => localizer["SharingOffline"],
            SharingFailure.Server => rejection.Detail ?? localizer["SharingFailed"],
            _ => localizer["SharingFailed"]
        };
}
