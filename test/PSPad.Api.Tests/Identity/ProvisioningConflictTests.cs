using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Api.Commands;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Identity;
using PSPad.Module.Identity.Provisioning;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Identity;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ProvisioningConflictTests(MongoFixture fixture)
{
    public sealed class ConflictBudget
    {
        int _remaining;

        public int Commits { get; private set; }

        public void Arm(int conflicts) => Interlocked.Exchange(ref _remaining, conflicts);

        public bool Spend()
        {
            Commits++;
            return Interlocked.Decrement(ref _remaining) >= 0;
        }
    }

    public sealed class ConflictingUnitOfWork(MongoUnitOfWork inner, ConflictBudget budget) : IUnitOfWork
    {
        readonly List<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> _staged = [];

        public void Stage(Aggregate aggregate, IReadOnlyList<DomainEvent> events) => _staged.Add((aggregate, events));

        public Task<bool> IsProcessedAsync(Guid commandId, CancellationToken ct) => inner.IsProcessedAsync(commandId, ct);

        public async Task CommitAsync(Guid commandId, Guid userId, CancellationToken ct)
        {
            var staged = _staged.ToList();
            _staged.Clear();

            if (budget.Spend())
            {
                throw new ConcurrencyConflictException(staged.FirstOrDefault().Aggregate?.Id ?? Guid.Empty);
            }

            foreach (var (aggregate, events) in staged)
            {
                inner.Stage(aggregate, events);
            }

            await inner.CommitAsync(commandId, userId, ct);
        }
    }

    readonly ConflictBudget _budget = new();

    ApiFactory Factory() => new(fixture, testServices: services =>
    {
        services.AddSingleton(_budget);
        services.AddScoped<MongoUnitOfWork>();
        services.Replace(ServiceDescriptor.Scoped<IUnitOfWork>(provider =>
            new ConflictingUnitOfWork(provider.GetRequiredService<MongoUnitOfWork>(), _budget)));
    });

    [Fact]
    public async Task AFirstSignInThatHitsAConflictIsRetriedAndProvisionsOnce()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var subject = Guid.NewGuid().ToString();
        using var client = factory.ClientFor(subject, name: "Zoe");
        _budget.Arm(CommandDispatcher.MostAttempts - 1);

        var response = await client.GetAsync("/api/me", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(ct);
        Assert.Equal(User.IdFor(subject), me!.UserId);
        await AssertProvisionedOnceAsync(factory, subject, ct);
    }

    [Fact]
    public async Task ConflictsPastTheLastAttemptAnswerServiceUnavailableWithRetryAfter()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var subject = Guid.NewGuid().ToString();
        using var client = factory.ClientFor(subject, name: "Zoe");
        _budget.Arm(CommandDispatcher.MostAttempts);

        var response = await client.GetAsync("/api/me", ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter?.Delta);
        Assert.Equal(CommandDispatcher.MostAttempts, _budget.Commits);
        Assert.Equal(0, await CountAsync(factory, "users", "_id", User.IdFor(subject), ct));
    }

    [Fact]
    public async Task TheSignInAfterAnExhaustedOneProvisionsNormally()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var subject = Guid.NewGuid().ToString();
        using var client = factory.ClientFor(subject, name: "Zoe");
        _budget.Arm(CommandDispatcher.MostAttempts);
        await client.GetAsync("/api/me", ct);

        var response = await client.GetAsync("/api/me", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertProvisionedOnceAsync(factory, subject, ct);
    }

    [Fact]
    public async Task ATimeZoneChangeThatHitsAConflictIsRetriedOnTheFreshUser()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var subject = Guid.NewGuid().ToString();
        using var client = factory.ClientFor(subject, name: "Zoe");
        (await client.GetAsync("/api/me", ct)).EnsureSuccessStatusCode();
        _budget.Arm(CommandDispatcher.MostAttempts - 1);

        var response = await client.PutAsJsonAsync("/api/me/timezone", new SetTimeZoneRequest("Europe/Warsaw"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(ct);
        Assert.Equal("Europe/Warsaw", me!.TimeZone);
    }

    [Fact]
    public async Task ARenameThatHitsAConflictIsRetried()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var subject = Guid.NewGuid().ToString();
        using (var before = factory.ClientFor(subject, name: "Zoe"))
        {
            (await before.GetAsync("/api/me", ct)).EnsureSuccessStatusCode();
        }

        using var client = factory.ClientFor(subject, name: "Zoe Renamed");
        _budget.Arm(CommandDispatcher.MostAttempts - 1);

        var response = await client.GetAsync("/api/me", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(ct);
        Assert.Equal("Zoe Renamed", me!.DisplayName);
    }

    [Fact]
    public async Task TwoTabsSigningInAtOnceStillEndWithOneUser()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var subject = Guid.NewGuid().ToString();
        var clients = Enumerable.Range(0, 4).Select(_ => factory.ClientFor(subject, name: "Zoe")).ToList();

        var responses = await Task.WhenAll(clients.Select(client => client.GetAsync("/api/me", ct)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await AssertProvisionedOnceAsync(factory, subject, ct);
        clients.ForEach(client => client.Dispose());
    }

    static async Task AssertProvisionedOnceAsync(ApiFactory factory, string subject, CancellationToken ct)
    {
        var userId = User.IdFor(subject);

        Assert.Equal(1, await CountAsync(factory, "users", "_id", userId, ct));
        Assert.Equal(1, await CountAsync(factory, "inboxes", "userId", userId, ct));
        Assert.Equal(
            ProvisioningPlan.SeedAreaNames("en").Count,
            await CountAsync(factory, "areas", "userId", userId, ct));
    }

    static Task<long> CountAsync(ApiFactory factory, string collection, string field, Guid id, CancellationToken ct) =>
        factory.Services.GetRequiredService<MongoContext>()
            .Collection<BsonDocument>(collection)
            .CountDocumentsAsync(
                Builders<BsonDocument>.Filter.Eq(field, new BsonBinaryData(id, GuidRepresentation.Standard)),
                cancellationToken: ct);
}
