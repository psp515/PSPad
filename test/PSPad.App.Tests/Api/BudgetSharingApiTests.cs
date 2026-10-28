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

    static (PSPadApiClient Client, HttpMessageHandler Handler) ClientAnswering(HttpStatusCode status, object? body = null)
    {
        var handler = new StubHandler(status, body);
        return (new PSPadApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), handler);
    }

    [Fact]
    public async Task JoiningReturnsTheBudget()
    {
        var client = ClientAnswering(HttpStatusCode.OK,
            new JoinBudgetResponse(BudgetId, new Dictionary<string, JsonElement[]>())).Client;

        var outcome = await client.JoinBudgetAsync("token", "K7M4PX");

        Assert.Equal(BudgetId, Assert.IsType<JoinOutcome.Joined>(outcome).Id);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, typeof(JoinOutcome.Invalid))]
    [InlineData(HttpStatusCode.Gone, typeof(JoinOutcome.Expired))]
    [InlineData(HttpStatusCode.TooManyRequests, typeof(JoinOutcome.TooManyTries))]
    public async Task AFailedJoinMapsItsStatus(HttpStatusCode status, Type outcome) =>
        Assert.IsType(outcome, await ClientAnswering(status).Client.JoinBudgetAsync("token", "K7M4PX"));

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

    sealed class StubHandler(HttpStatusCode status, object? body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = body is null ? null : JsonContent.Create(body) });
    }
}
