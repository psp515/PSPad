using MongoDB.Driver;
using PSPad.Api.Commands;
using PSPad.Api.Identity;
using PSPad.Api.Sync;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Lists;

namespace PSPad.Api.Endpoints;

public static class ListEndpoints
{
    public static void MapListEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("lists/join", async (
            JoinListRequest request, MongoContext context, CommandDispatcher dispatcher,
            SyncReader reader, ICurrentUser user, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return Results.NotFound();
            }

            var list = await context.Collection<TaskList>()
                .Find(Builders<TaskList>.Filter.Eq(candidate => candidate.InviteToken, request.Token) &
                      Builders<TaskList>.Filter.Eq(candidate => candidate.Deleted, false))
                .FirstOrDefaultAsync(ct);

            if (list is null)
            {
                return Results.NotFound();
            }

            var result = await dispatcher.RunAsync(
                new JoinTaskList(Guid.NewGuid(), user.UserId, list.Id, request.Token, user.DisplayName), ct);

            if (!result.Accepted)
            {
                return Results.NotFound();
            }

            var whole = await reader.ReadAsync(user.UserId, long.MaxValue, [list.Id], ct);
            return Results.Ok(new JoinListResponse(list.Id, whole.Documents));
        });
    }
}
