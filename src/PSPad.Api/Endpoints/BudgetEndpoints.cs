using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Api.Commands;
using PSPad.Api.Identity;
using PSPad.Api.Security;
using PSPad.Api.Sync;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Money.Budgets;

namespace PSPad.Api.Endpoints;

public static class BudgetEndpoints
{
    public static void MapBudgetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("budgets/join", async (
            JoinBudgetRequest request, MongoContext context, CommandDispatcher dispatcher,
            SyncReader reader, ICurrentUser user, IClock clock, HttpContext http, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Code))
            {
                return Results.NotFound();
            }

            var budget = await context.Collection<Budget>()
                .Find(Builders<Budget>.Filter.Eq(candidate => candidate.InviteToken, request.Token))
                .FirstOrDefaultAsync(ct);

            if (budget is null || budget.IsArchived)
            {
                return Results.NotFound();
            }

            var alreadyIn = budget.UserId == user.UserId || budget.HasMember(user.UserId);
            var check = alreadyIn ? InviteCheck.Open : budget.CheckInvite(request.Token, request.Code, clock.UtcNow);

            switch (check)
            {
                case InviteCheck.Unknown:
                    return Results.NotFound();
                case InviteCheck.Expired:
                    return Results.StatusCode(StatusCodes.Status410Gone);
                case InviteCheck.WrongCode:
                    SecurityLog.For(http).WrongInviteCode(user.UserId, "budget", budget.Id);
                    await dispatcher.RunAsync(Join(budget, request, user), ct);
                    return Results.NotFound();
            }

            var result = await dispatcher.RunAsync(Join(budget, request, user), ct);

            if (!result.Accepted)
            {
                return Results.NotFound();
            }

            var whole = await reader.ReadAsync(user.UserId, long.MaxValue, [budget.Id], ct);
            return Results.Ok(new JoinBudgetResponse(budget.Id, whole.Documents));
        }).RequireRateLimiting("join");

        app.MapPost("budgets/{id:guid}/share", (
            Guid id, ShareBudgetRequest request, IDocumentStore<Budget> budgets, CommandDispatcher dispatcher,
            ICurrentUser user, CancellationToken ct) =>
            ChangeAsync(id, Role.Owner, new ShareBudget(
                Guid.NewGuid(), user.UserId, id, request.Token, request.Code, user.DisplayName), budgets, dispatcher, user, ct));

        app.MapDelete("budgets/{id:guid}/share", (
            Guid id, IDocumentStore<Budget> budgets, CommandDispatcher dispatcher, ICurrentUser user, CancellationToken ct) =>
            ChangeAsync(id, Role.Owner, new StopSharingBudget(Guid.NewGuid(), user.UserId, id), budgets, dispatcher, user, ct));

        app.MapDelete("budgets/{id:guid}/members/{memberId:guid}", (
            Guid id, Guid memberId, IDocumentStore<Budget> budgets, CommandDispatcher dispatcher, ICurrentUser user,
            CancellationToken ct) =>
            ChangeAsync(id, Role.Owner, new RemoveBudgetMember(Guid.NewGuid(), user.UserId, id, memberId), budgets,
                dispatcher, user, ct));

        app.MapPost("budgets/{id:guid}/leave", (
            Guid id, IDocumentStore<Budget> budgets, CommandDispatcher dispatcher, ICurrentUser user, CancellationToken ct) =>
            ChangeAsync(id, Role.Member, new LeaveBudget(Guid.NewGuid(), user.UserId, id), budgets, dispatcher, user, ct));
    }

    enum Role
    {
        Owner,
        Member
    }

    // One 404 for a missing budget and for a caller without the role, like join, so ids cannot be probed.
    static async Task<IResult> ChangeAsync(
        Guid budgetId, Role role, ICommand command, IDocumentStore<Budget> budgets, CommandDispatcher dispatcher,
        ICurrentUser user, CancellationToken ct)
    {
        var budget = await budgets.LoadAsync(budgetId, ct);
        var allowed = budget is not null &&
                      (role == Role.Owner ? budget.UserId == user.UserId : budget.HasMember(user.UserId));

        if (!allowed)
        {
            return Results.NotFound();
        }

        var result = await dispatcher.RunAsync(command, ct);
        return result.Accepted
            ? Results.NoContent()
            : Results.Problem(detail: result.Rejection, statusCode: StatusCodes.Status409Conflict);
    }

    static JoinBudget Join(Budget budget, JoinBudgetRequest request, ICurrentUser user) =>
        new(Guid.NewGuid(), user.UserId, budget.Id, request.Token, request.Code, user.DisplayName);
}
