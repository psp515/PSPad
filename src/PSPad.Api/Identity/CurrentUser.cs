using System.Security.Claims;
using PSPad.Abstractions;

namespace PSPad.Api.Identity;

public interface ICurrentUser
{
    string Subject { get; }

    Guid UserId { get; }

    string TimeZoneHint { get; }

    string LanguageHint { get; }

    string DisplayName { get; }

    string Email { get; }
}

public sealed class ClaimsCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string Subject =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? accessor.HttpContext?.User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("The token carries no subject.");

    public Guid UserId => Module.Identity.User.IdFor(Subject);

    public string TimeZoneHint =>
        accessor.HttpContext?.User.FindFirstValue("zoneinfo")
        ?? KnownZone(accessor.HttpContext?.Request.Headers[Contracts.TimeZoneHint.Header].ToString())
        ?? "Etc/UTC";

    public string LanguageHint =>
        SupportedLanguages.FromBrowser(accessor.HttpContext?.Request.Headers.AcceptLanguage.ToString());

    public string DisplayName =>
        accessor.HttpContext?.User.FindFirstValue("name")
        ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
        ?? accessor.HttpContext?.User.FindFirstValue("preferred_username")
        ?? Subject;

    public string Email =>
        accessor.HttpContext?.User.FindFirstValue("email") ?? "";

    static readonly HashSet<string> UtcAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "UTC", "UCT", "Etc/UTC", "Etc/UCT", "Universal", "Etc/Universal", "Zulu", "Etc/Zulu",
        "GMT", "Etc/GMT", "Etc/GMT0", "Etc/GMT+0", "Etc/GMT-0", "Greenwich", "Etc/Greenwich",
    };

    static string? KnownZone(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null
        : UtcAliases.Contains(id) ? "Etc/UTC"
        : TimeZoneInfo.TryFindSystemTimeZoneById(id, out _) ? id
        : null;
}
