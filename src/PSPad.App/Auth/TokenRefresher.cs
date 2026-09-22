using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.App.Auth;

public sealed class TokenRefresher(HttpClient http, IClock clock, string authority, string clientId)
{
    public string? AccessToken { get; private set; }

    public DateTimeOffset AccessTokenExpiresAt { get; private set; }

    public async Task<RefreshOutcome> RefreshAsync(string refreshToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await http.PostAsync(
                $"{authority.TrimEnd('/')}/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = clientId,
                    ["refresh_token"] = refreshToken
                }));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new RefreshOutcome.Offline();
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadAsStringAsync();
            return error.Contains("invalid_grant")
                ? new RefreshOutcome.Revoked()
                : new RefreshOutcome.Offline();
        }

        if (!response.IsSuccessStatusCode)
        {
            return new RefreshOutcome.Offline();
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();

        if (payload is null)
        {
            return new RefreshOutcome.Offline();
        }

        AccessToken = payload.AccessToken;
        AccessTokenExpiresAt = clock.UtcNow.AddSeconds(payload.ExpiresIn);

        return new RefreshOutcome.Renewed(payload.AccessToken, AccessTokenExpiresAt, payload.RefreshToken);
    }

    sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string RefreshToken);
}
