using System.Security.Claims;

namespace PSPad.Api.Identity;

public interface ICurrentUser
{
    string Subject { get; }

    Guid UserId { get; }

    string TimeZoneHint { get; }
}

public sealed class ClaimsCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string Subject =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? accessor.HttpContext?.User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("The token carries no subject.");

    public Guid UserId => Module.Identity.User.IdFor(Subject);

    public string TimeZoneHint =>
        accessor.HttpContext?.User.FindFirstValue("zoneinfo") ?? "Etc/UTC";
}
