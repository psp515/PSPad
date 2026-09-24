using PSPad.Api.Identity;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapDelete("account", async (
            ICurrentUser current, MongoContext context, IKeycloakAdminClient keycloak,
            ILogger<DeleteAccountResponse> logger, CancellationToken ct) =>
        {
            await UserDataWipe.RunAsync(context, current.UserId, ct);

            bool keycloakRemoved;

            try
            {
                keycloakRemoved = await keycloak.DeleteUserAsync(current.Subject, ct);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception, "Removing the Keycloak login for {Subject} failed unexpectedly.", current.Subject);
                keycloakRemoved = false;
            }

            return Results.Ok(new DeleteAccountResponse(keycloakRemoved));
        });
    }
}
