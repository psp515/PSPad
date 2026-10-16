using System.Globalization;
using System.Security.Claims;

namespace PSPad.Api.Live;

public static class LiveStreamEnd
{
    public static DateTimeOffset For(ClaimsPrincipal user, DateTimeOffset openedAt, TimeSpan maxStream)
    {
        var cap = openedAt + maxStream;

        if (!long.TryParse(user.FindFirstValue("exp"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var exp))
        {
            return cap;
        }

        var expires = DateTimeOffset.FromUnixTimeSeconds(exp);
        return expires < cap ? expires : cap;
    }
}
