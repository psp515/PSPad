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

            return Results.Ok(new MeResponse(user.Id, user.DisplayName, user.TimeZone));
        });
    }
}
