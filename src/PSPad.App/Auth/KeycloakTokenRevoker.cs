namespace PSPad.App.Auth;

public sealed class KeycloakTokenRevoker(HttpClient http, string authority, string clientId)
{
    public async Task<RevocationOutcome> RevokeAsync(string refreshToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await http.PostAsync(
                $"{authority.TrimEnd('/')}/protocol/openid-connect/revoke",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["token"] = refreshToken,
                    ["token_type_hint"] = "refresh_token"
                }));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return RevocationOutcome.Unreachable;
        }

        if (response.IsSuccessStatusCode)
        {
            return RevocationOutcome.Revoked;
        }

        return (int)response.StatusCode >= 500 ? RevocationOutcome.Unreachable : RevocationOutcome.Refused;
    }
}
