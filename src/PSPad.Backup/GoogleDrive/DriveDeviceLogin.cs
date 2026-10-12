using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PSPad.Backup.GoogleDrive;

public sealed class DriveDeviceLogin(HttpClient http, TimeProvider clock, TextWriter prompt)
{
    const string Scope = "https://www.googleapis.com/auth/drive.file";
    const string DeviceGrant = "urn:ietf:params:oauth:grant-type:device_code";

    public async Task<string> RefreshTokenAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        using var codeResponse = await http.PostAsync("https://oauth2.googleapis.com/device/code",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = clientId, ["scope"] = Scope }), ct);
        var code = await codeResponse.Content.ReadFromJsonAsync<DeviceCode>(ct);
        if (!codeResponse.IsSuccessStatusCode || code?.DeviceCodeValue is null)
        {
            throw new BackupFailure(
                "Google refused the device code request; the OAuth client must be of type 'TVs and Limited Input devices'");
        }

        await prompt.WriteLineAsync($"Open {code.VerificationUrl} and enter the code {code.UserCode}");
        var interval = TimeSpan.FromSeconds(Math.Max(code.Interval, 1));
        var deadline = clock.GetUtcNow().AddSeconds(code.ExpiresIn);

        while (clock.GetUtcNow() < deadline)
        {
            await Task.Delay(interval, clock, ct);
            using var tokenResponse = await http.PostAsync("https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["device_code"] = code.DeviceCodeValue,
                    ["grant_type"] = DeviceGrant
                }), ct);
            var token = await tokenResponse.Content.ReadFromJsonAsync<Token>(ct);

            switch (token?.Error)
            {
                case null when token?.RefreshToken is { } refreshToken:
                    return refreshToken;
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    continue;
                default:
                    throw new BackupFailure($"Google Drive sign-in failed: {token?.Error ?? "no refresh token returned"}");
            }
        }

        throw new BackupFailure("Google Drive sign-in timed out; run gdrive-login again");
    }

    sealed record DeviceCode(
        [property: JsonPropertyName("device_code")] string? DeviceCodeValue,
        [property: JsonPropertyName("user_code")] string? UserCode,
        [property: JsonPropertyName("verification_url")] string? VerificationUrl,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("interval")] int Interval);

    sealed record Token(
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("error")] string? Error);
}
