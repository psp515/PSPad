using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PSPad.App.Api;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Api;

[UnitTest]
public class BudgetSharingApiTests
{
    static readonly Guid BudgetId = Guid.NewGuid();

    static (PSPadApiClient Client, RecordingHandler Handler) ClientAnswering(HttpStatusCode status, object? body = null)
    {
        var handler = new RecordingHandler(status, body);
        return (new PSPadApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), handler);
    }

    [Fact]
    public async Task JoiningPostsTheCodeAndReturnsTheBudget()
    {
        var (client, handler) = ClientAnswering(HttpStatusCode.OK,
            new JoinBudgetResponse(BudgetId, new Dictionary<string, JsonElement[]>()));

        var outcome = await client.JoinBudgetAsync("token", "K7M4PX");

        Assert.Equal(BudgetId, Assert.IsType<JoinOutcome.Joined>(outcome).Id);
        Assert.Equal("POST /api/budgets/join", handler.Call);
        Assert.Equal(new JoinBudgetRequest("token", "K7M4PX"), JsonSerializer.Deserialize<JoinBudgetRequest>(
            handler.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, typeof(JoinOutcome.Invalid))]
    [InlineData(HttpStatusCode.Gone, typeof(JoinOutcome.Expired))]
    [InlineData(HttpStatusCode.TooManyRequests, typeof(JoinOutcome.TooManyTries))]
    public async Task AFailedJoinMapsItsStatus(HttpStatusCode status, Type outcome) =>
        Assert.IsType(outcome, await ClientAnswering(status).Client.JoinBudgetAsync("token", "K7M4PX"));

    [Fact]
    public async Task EachChangeHitsItsEndpoint()
    {
        var memberId = Guid.NewGuid();
        var (client, handler) = ClientAnswering(HttpStatusCode.NoContent);

        Assert.Null(await client.ShareBudgetAsync(BudgetId, "token", "K7M4PX"));
        Assert.Equal($"POST /api/budgets/{BudgetId}/share", handler.Call);
        Assert.Null(await client.StopSharingBudgetAsync(BudgetId));
        Assert.Equal($"DELETE /api/budgets/{BudgetId}/share", handler.Call);
        Assert.Null(await client.RemoveBudgetMemberAsync(BudgetId, memberId));
        Assert.Equal($"DELETE /api/budgets/{BudgetId}/members/{memberId}", handler.Call);
        Assert.Null(await client.LeaveBudgetAsync(BudgetId));
        Assert.Equal($"POST /api/budgets/{BudgetId}/leave", handler.Call);
    }

    [Fact]
    public async Task AConflictReturnsItsDetail() =>
        Assert.Equal(new SharingRejection(SharingFailure.Server, "This budget is archived."), await ClientAnswering(HttpStatusCode.Conflict,
            new { detail = "This budget is archived." }).Client.ShareBudgetAsync(BudgetId, "token", "K7M4PX"));

    [Fact]
    public async Task ANotFoundSaysTheBudgetIsGone() =>
        Assert.Equal(new SharingRejection(SharingFailure.BudgetGone),
            await ClientAnswering(HttpStatusCode.NotFound).Client.LeaveBudgetAsync(BudgetId));

    [Fact]
    public async Task AServerErrorIsAGenericFailure() =>
        Assert.Equal(new SharingRejection(SharingFailure.Failed),
            await ClientAnswering(HttpStatusCode.InternalServerError).Client.LeaveBudgetAsync(BudgetId));

    sealed class RecordingHandler(HttpStatusCode status, object? body) : HttpMessageHandler
    {
        public string? Call { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Call = $"{request.Method} {request.RequestUri!.AbsolutePath}";
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = body is null ? null : JsonContent.Create(body) };
        }
    }
}
