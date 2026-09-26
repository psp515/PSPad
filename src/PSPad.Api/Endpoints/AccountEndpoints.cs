using PSPad.Api.Identity;
using PSPad.Contracts;
using PSPad.Infrastructure.Events;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Endpoints;

public static class AccountEndpoints
{
    static readonly TimeSpan ProjectionDrainTimeout = TimeSpan.FromSeconds(10);

    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapDelete("account", async (
            ICurrentUser current, MongoContext context, IKeycloakAdminClient keycloak,
            ChannelDomainEventDispatcher dispatcher, ILogger<DeleteAccountResponse> logger, CancellationToken ct) =>
        {
            await DrainProjectionsAsync(dispatcher, logger, ct);
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

    // The pump projects committed events after the request that wrote them returns, so wiping first
    // lets a late projection write Statistics documents back for a user who no longer exists.
    static async Task DrainProjectionsAsync(
        ChannelDomainEventDispatcher dispatcher, ILogger logger, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProjectionDrainTimeout);

        try
        {
            await dispatcher.DrainAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Projections did not drain within {Timeout}; wiping anyway.", ProjectionDrainTimeout);
        }
    }
}
