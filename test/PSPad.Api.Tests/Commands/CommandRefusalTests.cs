using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;

namespace PSPad.Api.Tests.Commands;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class CommandRefusalTests(MongoFixture fixture)
{
    [Fact]
    public async Task AnEnvelopeForSomebodyElsesUserIdIsRefusedAndStoresNothing()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        await SignInAsync(client, ct);
        var areaId = Guid.NewGuid();

        var result = Assert.Single(await TrySendAsync(client, ct,
            new CreateArea(Guid.NewGuid(), Guid.NewGuid(), areaId, "Home", 0)));

        Assert.False(result.Accepted);
        Assert.True(result.Unrecoverable);
        Assert.Equal("That command is for a different user.", result.Rejection);
        Assert.DoesNotContain(await SyncedAsync(client, "areas", ct), row => row.GetProperty("id").GetGuid() == areaId);
    }

    [Fact]
    public async Task AnUnknownCommandTypeIsRefusedRatherThanFailingTheRequest()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());

        var result = Assert.Single(await PostRawAsync(client, """[{"type":"DropDatabase","payload":{}}]""", ct));

        Assert.False(result.Accepted);
        Assert.True(result.Unrecoverable);
        Assert.Equal("Unknown command DropDatabase.", result.Rejection);
    }

    [Fact]
    public async Task ANullPayloadIsRefusedAsMalformed()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());

        var result = Assert.Single(await PostRawAsync(client, """[{"type":"CreateArea","payload":null}]""", ct));

        Assert.False(result.Accepted);
        Assert.True(result.Unrecoverable);
        Assert.Equal("Malformed payload for CreateArea.", result.Rejection);
    }

    [Fact]
    public async Task AMissingPayloadIsRefusedAsMalformed()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());

        var result = Assert.Single(await PostRawAsync(client, """[{"type":"CreateArea"}]""", ct));

        Assert.False(result.Accepted);
        Assert.True(result.Unrecoverable);
        Assert.Equal("Malformed payload for CreateArea.", result.Rejection);
    }

    [Fact]
    public async Task AWronglyTypedFieldIsRefusedAsMalformed()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var payload = JsonSerializer.Serialize(new
        {
            commandId = Guid.NewGuid(), userId = Guid.NewGuid(), areaId = "not-a-guid", name = "Home", position = 0
        });

        var result = Assert.Single(await PostRawAsync(client, $$"""[{"type":"CreateArea","payload":{{payload}}}]""", ct));

        Assert.False(result.Accepted);
        Assert.True(result.Unrecoverable);
        Assert.Equal("Malformed payload for CreateArea.", result.Rejection);
    }

    static async Task<CommandResponse[]> PostRawAsync(HttpClient client, string body, CancellationToken ct)
    {
        var response = await client.PostAsync(
            "/api/commands", new StringContent(body, Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!;
    }
}
