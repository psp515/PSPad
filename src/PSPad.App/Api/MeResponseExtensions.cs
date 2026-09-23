using PSPad.Contracts;

namespace PSPad.App.Api;

public static class MeResponseExtensions
{
    public static MeResponse? Sanitized(this MeResponse? me) =>
        me is null || me.UserId == Guid.Empty
            ? null
            : me with
            {
                DisplayName = me.DisplayName ?? "",
                Email = me.Email ?? "",
                TimeZone = string.IsNullOrEmpty(me.TimeZone) ? "Etc/UTC" : me.TimeZone
            };
}
