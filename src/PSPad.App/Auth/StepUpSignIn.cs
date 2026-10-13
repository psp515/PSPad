using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace PSPad.App.Auth;

public static class StepUpSignIn
{
    public const string DeleteAccountQuery = "deleteAccount";

    public const string DeleteAccountReturn = "settings?" + DeleteAccountQuery + "=confirm";

    public static void Begin(NavigationManager navigation, string returnPath)
    {
        var request = new InteractiveRequestOptions
        {
            Interaction = InteractionType.SignIn,
            ReturnUrl = navigation.ToAbsoluteUri(returnPath).ToString()
        };
        request.TryAddAdditionalParameter("prompt", "login");
        request.TryAddAdditionalParameter("max_age", "0");
        navigation.NavigateToLogin("authentication/login", request);
    }
}
