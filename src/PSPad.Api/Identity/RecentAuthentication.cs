using System.Globalization;
using System.Security.Claims;

namespace PSPad.Api.Identity;

public static class RecentAuthentication
{
    public const string Claim = "auth_time";

    public static bool Holds(ClaimsPrincipal user, DateTimeOffset now, TimeSpan maxAge)
    {
        if (!long.TryParse(user.FindFirstValue(Claim), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var seconds))
        {
            return false;
        }

        return now - DateTimeOffset.FromUnixTimeSeconds(seconds) <= maxAge;
    }
}
