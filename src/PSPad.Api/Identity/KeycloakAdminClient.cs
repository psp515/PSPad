using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace PSPad.Api.Identity;

public sealed class KeycloakAdminClient(HttpClient http, IOptions<KeycloakAdminOptions> options) : IKeycloakAdminClient
{
    const int MaxAttempts = 3;

    public async Task<bool> DeleteUserAsync(string subject, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (await TryDeleteAsync(subject, ct))
            {
                return true;
            }
        }

        return false;
    }

    async Task<bool> TryDeleteAsync(string subject, CancellationToken ct)
    {
        var token = await GetAdminTokenAsync(ct);

        if (token is null)
        {
            return false;
        }

        var (baseUrl, realm) = SplitAuthority(options.Value.Authority);
        using var request = new HttpRequestMessage(
            HttpMethod.Delete, $"{baseUrl}/admin/realms/{realm}/users/{subject}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;

        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }

        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
    }

    async Task<string?> GetAdminTokenAsync(CancellationToken ct)
    {
        var (baseUrl, _) = SplitAuthority(options.Value.Authority);

        HttpResponseMessage response;

        try
        {
            response = await http.PostAsync(
                $"{baseUrl}/realms/master/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = "admin-cli",
                    ["username"] = options.Value.AdminUser,
                    ["password"] = options.Value.AdminPassword
                }), ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            return payload?.AccessToken;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static (string BaseUrl, string Realm) SplitAuthority(string authority)
    {
        const string marker = "/realms/";
        var index = authority.IndexOf(marker, StringComparison.Ordinal);

        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Keycloak authority '{authority}' is not shaped like '<base>/realms/<realm>'.");
        }

        return (authority[..index], authority[(index + marker.Length)..].TrimEnd('/'));
    }

    sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);
}
