using PSPad.Api.Commands;
using PSPad.Api.Identity;
using PSPad.Contracts;

namespace PSPad.Api.Endpoints;

public static class CommandEndpoints
{
    public static void MapCommandEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("commands", async (
            CommandEnvelope[] envelopes,
            CommandDispatcher dispatcher,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (envelopes.Length > CommandBatch.MaxEnvelopes)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: $"A batch holds at most {CommandBatch.MaxEnvelopes} commands.");
            }

            var responses = new List<CommandResponse>(envelopes.Length);

            foreach (var envelope in envelopes)
            {
                responses.Add(await dispatcher.DispatchAsync(envelope, user.UserId, ct));
            }

            return Results.Ok(responses);
        }).RequireRateLimiting("commands");
    }
}
