using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.App.State.Investments;
using PSPad.Module.Investments.Reading;

namespace PSPad.App.Components.Investments;

public sealed record PositionText(string? Warning, bool OffersFixSymbol, decimal? UnrealizedShare)
{
    public static PositionText For(Position position, IStringLocalizer<InvestmentsStrings> text) =>
        new(position.Warning switch
            {
                PositionWarning.NoPrice => text["NoPrice"].Value,
                PositionWarning.Oversold => text["OversoldBy", InvestmentText.Quantity(position.OversoldBy)].Value,
                _ => null
            },
            position.Warning == PositionWarning.NoPrice,
            position.Cost > 0 ? position.Unrealized / position.Cost : null);
}
