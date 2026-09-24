using PSPad.Api.Identity;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapDelete("account", async (
            ICurrentUser current, MongoContext context, IKeycloakAdminClient keycloak, CancellationToken ct) =>
        {
            await UserDataWipe.RunAsync(context, current.UserId, ct);
            var keycloakRemoved = await keycloak.DeleteUserAsync(current.Subject, ct);
            return Results.Ok(new DeleteAccountResponse(keycloakRemoved));
        });
    }
}
