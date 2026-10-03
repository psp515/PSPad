using System.Security.Cryptography;

namespace PSPad.App.State;

public static class InviteToken
{
    public static string New() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace('+', '-').Replace('/', '_');

    public static string LinkFor(string baseUri, string token) => $"{baseUri.TrimEnd('/')}/join/{token}";
}
