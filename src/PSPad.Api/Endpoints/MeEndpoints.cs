using PSPad.Abstractions;
using PSPad.Api.Identity;
using PSPad.Contracts;

namespace PSPad.Api.Endpoints;

public static class MeEndpoints
{
    public static void MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("me", async (
            ICurrentUser current, UserProvisioner provisioner, CancellationToken ct) =>
        {
            var user = await provisioner.EnsureAsync(
                current.Subject, current.DisplayName, current.TimeZoneHint, ct);

            if (current.DisplayName != current.Subject)
            {
                user = await provisioner.RenameAsync(user, current.DisplayName, ct);
            }

            return Results.Ok(new MeResponse(user.Id, user.DisplayName, current.Email, user.TimeZone));
        });

        app.MapPut("me/timezone", async (
            SetTimeZoneRequest request,
            ICurrentUser current,
            UserProvisioner provisioner,
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

            return Results.Ok(new MeResponse(user.Id, user.DisplayName, current.Email, user.TimeZone));
        });
    }
}
