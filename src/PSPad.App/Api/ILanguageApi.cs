using PSPad.Contracts;

namespace PSPad.App.Api;

public interface ILanguageApi
{
    Task<MeResponse?> SetLanguageAsync(string language);
}
