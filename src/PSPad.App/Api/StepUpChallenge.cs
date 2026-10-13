using System.Net;

namespace PSPad.App.Api;

public static class StepUpChallenge
{
    const string Error = "insufficient_user_authentication";

    public static bool Names(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.Unauthorized
        && response.Headers.WwwAuthenticate.Any(challenge =>
            challenge.Parameter?.Contains($"error=\"{Error}\"", StringComparison.Ordinal) == true);
}
