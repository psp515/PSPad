using Microsoft.Extensions.Localization;
using MudBlazor;
using PSPad.App.Resources;

namespace PSPad.App.Localization;

public sealed class PSPadMudLocalizer(IStringLocalizer<MudStrings> strings) : MudLocalizer
{
    public override LocalizedString this[string key] => strings[key];
}
