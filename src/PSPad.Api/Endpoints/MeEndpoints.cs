using PSPad.Abstractions;
using PSPad.Api.Identity;
using PSPad.Contracts;

namespace PSPad.Api.Endpoints;

public static class MeEndpoints
{
    public static void MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("me", async (
            ICurrentUser current, UserProvisioner provisioner, ILogger<MeResponse> logger, CancellationToken ct) =>
        {
            var user = await provisioner.EnsureAsync(
                current.Subject, current.DisplayName, current.TimeZoneHint, ct);

            if (current.DisplayName != current.Subject)
            {
                user = await provisioner.RenameAsync(user, current.DisplayName, ct);
            }

            WarnIfUnexpectedlyEmpty(logger, user.Id, user.DisplayName, user.TimeZone);
            return Results.Ok(new MeResponse(user.Id, user.DisplayName, current.Email, user.TimeZone));
        });

        app.MapPut("me/timezone", async (
            SetTimeZoneRequest request,
            ICurrentUser current,
            UserProvisioner provisioner,
            ILogger<MeResponse> logger,
            CancellationToken ct) =>
        {
            var user = await provisioner.EnsureAsync(
                current.Subject, current.DisplayName, current.TimeZoneHint, ct);

            try
            {
                user = await provisioner.SetTimeZoneAsync(user, request.TimeZone, ct);
            }
            catch (DomainRejectedException rejected)
            {
                return Results.BadRequest(rejected.Message);
            }

            WarnIfUnexpectedlyEmpty(logger, user.Id, user.DisplayName, user.TimeZone);
            return Results.Ok(new MeResponse(user.Id, user.DisplayName, current.Email, user.TimeZone));
        });
    }

    // DisplayName and TimeZone always fall back to a non-empty value on the User aggregate
    // (RequireDisplayName rejects blank, ProvisionUser falls back through claims to the subject) --
    // seeing either empty here means something upstream broke an invariant this class relies on,
    // and is worth catching in production logs rather than only in the client's own crash reports.
    static void WarnIfUnexpectedlyEmpty(ILogger logger, Guid userId, string displayName, string timeZone)
    {
        if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(timeZone))
        {
            logger.LogWarning(
                "Building /api/me response with an unexpectedly empty field for user {UserId}: " +
                "DisplayName='{DisplayName}' TimeZone='{TimeZone}'",
                userId, displayName, timeZone);
        }
    }
}
