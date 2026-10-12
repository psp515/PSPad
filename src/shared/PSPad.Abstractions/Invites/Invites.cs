using System.Security.Cryptography;
using System.Text;

namespace PSPad.Abstractions;

public static class Invites
{
    public const int ShortestToken = 22;

    public const int MostWrongCodes = 5;

    public static readonly TimeSpan Life = TimeSpan.FromMinutes(30);

    public static bool IsLiveAt(string? token, string? code, DateTimeOffset? expiresAt, DateTimeOffset now) =>
        token is not null && code is not null && expiresAt is { } until && now < until;

    public static InviteCheck Check(
        string? storedToken, string? storedCode, DateTimeOffset? expiresAt, string token, string code, DateTimeOffset now)
    {
        if (!SameSecret(storedToken, token))
        {
            return InviteCheck.Unknown;
        }

        var live = IsLiveAt(storedToken, storedCode, expiresAt, now);

        if (!SameSecret(storedCode, InviteCodes.Normalize(code)))
        {
            return live ? InviteCheck.WrongCode : InviteCheck.Unknown;
        }

        return live ? InviteCheck.Open : InviteCheck.Expired;
    }

    public static string RequireToken(string? token) =>
        string.IsNullOrWhiteSpace(token) || token.Length < ShortestToken
            ? throw new DomainRejectedException("An invite link needs a longer token.")
            : token;

    public static string RequireCode(string? code)
    {
        var normalized = InviteCodes.Normalize(code);
        return InviteCodes.IsWellFormed(normalized)
            ? normalized
            : throw new DomainRejectedException("An invite needs a 6-character code.");
    }

    static bool SameSecret(string? stored, string offered) =>
        stored is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(offered));
}
