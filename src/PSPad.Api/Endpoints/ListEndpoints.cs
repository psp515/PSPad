using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Api.Commands;
using PSPad.Api.Identity;
using PSPad.Api.Security;
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
            SyncReader reader, ICurrentUser user, IClock clock, HttpContext http, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Code))
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

            var alreadyIn = list.UserId == user.UserId || list.HasMember(user.UserId);
            var check = alreadyIn ? InviteCheck.Open : list.CheckInvite(request.Token, request.Code, clock.UtcNow);

            switch (check)
            {
                case InviteCheck.Unknown:
                    return Results.NotFound();
                case InviteCheck.Expired:
                    return Results.StatusCode(StatusCodes.Status410Gone);
                case InviteCheck.WrongCode:
                    SecurityLog.For(http).WrongInviteCode(user.UserId, "list", list.Id);
                    await dispatcher.RunAsync(Join(list, request, user), ct);
                    return Results.NotFound();
            }

            var result = await dispatcher.RunAsync(Join(list, request, user), ct);

            if (!result.Accepted)
            {
                return Results.NotFound();
            }

            var whole = await reader.ReadAsync(user.UserId, long.MaxValue, [list.Id], ct);
            return Results.Ok(new JoinListResponse(list.Id, whole.Documents));
        }).RequireRateLimiting("join");

        app.MapPost("lists/{id:guid}/share", (
            Guid id, ShareListRequest request, IDocumentStore<TaskList> lists, CommandDispatcher dispatcher,
            ICurrentUser user, CancellationToken ct) =>
            ChangeAsync(id, Role.Owner, new ShareTaskList(
                Guid.NewGuid(), user.UserId, id, request.Token, request.Code, user.DisplayName), lists, dispatcher, user, ct));

        app.MapDelete("lists/{id:guid}/share", (
            Guid id, IDocumentStore<TaskList> lists, CommandDispatcher dispatcher, ICurrentUser user, CancellationToken ct) =>
            ChangeAsync(id, Role.Owner, new StopSharingTaskList(Guid.NewGuid(), user.UserId, id), lists, dispatcher, user, ct));

        app.MapDelete("lists/{id:guid}/members/{memberId:guid}", (
            Guid id, Guid memberId, IDocumentStore<TaskList> lists, CommandDispatcher dispatcher, ICurrentUser user,
            CancellationToken ct) =>
            ChangeAsync(id, Role.Owner, new RemoveListMember(Guid.NewGuid(), user.UserId, id, memberId), lists, dispatcher, user, ct));

        app.MapPost("lists/{id:guid}/leave", (
            Guid id, IDocumentStore<TaskList> lists, CommandDispatcher dispatcher, ICurrentUser user, CancellationToken ct) =>
            ChangeAsync(id, Role.Member, new LeaveTaskList(Guid.NewGuid(), user.UserId, id), lists, dispatcher, user, ct));
    }

    enum Role
    {
        Owner,
        Member
    }

    // One 404 for a missing list and for a caller without the role, like join, so ids cannot be probed.
    static async Task<IResult> ChangeAsync(
        Guid listId, Role role, ICommand command, IDocumentStore<TaskList> lists, CommandDispatcher dispatcher,
        ICurrentUser user, CancellationToken ct)
    {
        var list = await lists.LoadAsync(listId, ct);
        var allowed = list is { Deleted: false } &&
                      (role == Role.Owner ? list.UserId == user.UserId : list.HasMember(user.UserId));

        if (!allowed)
        {
            return Results.NotFound();
        }

        var result = await dispatcher.RunAsync(command, ct);
        return result.Accepted
            ? Results.NoContent()
            : Results.Problem(detail: result.Rejection, statusCode: StatusCodes.Status409Conflict);
    }

    static JoinTaskList Join(TaskList list, JoinListRequest request, ICurrentUser user) =>
        new(Guid.NewGuid(), user.UserId, list.Id, request.Token, request.Code, user.DisplayName);
}
