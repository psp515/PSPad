using PSPad.Contracts;

namespace PSPad.App.Api;

public static class MeResponseExtensions
{
    public static MeResponse Sanitized(this MeResponse me) => me with
    {
        DisplayName = me.DisplayName ?? "",
        Email = me.Email ?? "",
        TimeZone = string.IsNullOrEmpty(me.TimeZone) ? "Etc/UTC" : me.TimeZone
    };
}
