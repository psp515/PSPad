using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;
using PSPad.Module.Investments.Operations;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Api.Tests.Investments;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class InvestmentCommandTests(MongoFixture fixture)
{
    static readonly DateTimeOffset TradedAt = new(2026, 10, 1, 14, 30, 0, TimeSpan.Zero);

    static MoneyValue Pln(decimal amount) => new(amount, "PLN", 1m, DateOnly.FromDateTime(TradedAt.UtcDateTime));

    static RecordInvestmentOperation ABuy(Guid user, Guid accountId, Guid? operationId = null) =>
        new(Guid.NewGuid(), user, operationId ?? Guid.NewGuid(), accountId, OperationKind.Buy, TradedAt, "PQR.PL", 2m,
            60m, Pln(120m), null, OperationSource.Manual, null, null);

    static async Task<(HttpClient Client, Guid User, Guid AccountId)> WithAnAccountAsync(ApiFactory factory, CancellationToken ct)
    {
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var accountId = Guid.NewGuid();
        await SendAsync(client, ct, new CreateInvestmentAccount(Guid.NewGuid(), user, accountId, "IKE", "PLN", Broker.Xtb, null));
        return (client, user, accountId);
    }

    [Fact]
    public async Task ARecordedOperationSyncsWithItsAccountAndIsCounted()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, accountId) = await WithAnAccountAsync(factory, ct);

        await SendAsync(client, ct, ABuy(user, accountId));

        var operation = Assert.Single(await SyncedAsync(client, "investmentoperations", ct));
        Assert.Equal(accountId, operation.GetProperty("accountId").GetGuid());
        Assert.Equal("PQR.PL", operation.GetProperty("ticker").GetString());
        Assert.Equal(120m, operation.GetProperty("money").GetProperty("amount").GetDecimal());
        var account = Assert.Single(await SyncedAsync(client, "investmentaccounts", ct));
        Assert.Equal(1, account.GetProperty("operationCount").GetInt32());
    }

    [Fact]
    public async Task AStrangersOperationOnMyAccountIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, _, accountId) = await WithAnAccountAsync(factory, ct);
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        var strangerId = await SignInAsync(stranger, ct);

        var result = Assert.Single(await TrySendAsync(stranger, ct, ABuy(strangerId, accountId)));

        Assert.False(result.Accepted);
        Assert.Equal("That account does not exist.", result.Rejection);
        Assert.Empty(await SyncedAsync(owner, "investmentoperations", ct));
        Assert.Empty(await SyncedAsync(stranger, "investmentoperations", ct));
    }

    [Fact]
    public async Task ImportingTheSameRowTwiceKeepsOneOperationAndCountsItOnce()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, accountId) = await WithAnAccountAsync(factory, ct);
        RecordInvestmentOperation Imported() => ABuy(user, accountId, InvestmentOperation.IdFor(accountId, 900000001)) with
        {
            Source = OperationSource.Xtb,
            ExternalId = "900000001"
        };

        await SendAsync(client, ct, Imported());
        await SendAsync(client, ct, Imported());

        Assert.Single(await SyncedAsync(client, "investmentoperations", ct));
        var account = Assert.Single(await SyncedAsync(client, "investmentaccounts", ct));
        Assert.Equal(1, account.GetProperty("operationCount").GetInt32());
    }

    [Fact]
    public async Task AnArchivedAccountRejectsAnOperation()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, accountId) = await WithAnAccountAsync(factory, ct);
        await SendAsync(client, ct, new ArchiveInvestmentAccount(Guid.NewGuid(), user, accountId));

        var result = Assert.Single(await TrySendAsync(client, ct, ABuy(user, accountId)));

        Assert.False(result.Accepted);
        Assert.Equal("This account is archived.", result.Rejection);
        Assert.Empty(await SyncedAsync(client, "investmentoperations", ct));
    }

    [Fact]
    public async Task The33rdAccountIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        await SendAsync(client, ct, Enumerable.Range(0, 32)
            .Select(index => (object)new CreateInvestmentAccount(Guid.NewGuid(), user, Guid.NewGuid(), $"Account {index}",
                "PLN", Broker.Other, null))
            .ToArray());

        var result = Assert.Single(await TrySendAsync(client, ct,
            new CreateInvestmentAccount(Guid.NewGuid(), user, Guid.NewGuid(), "One more", "PLN", Broker.Other, null)));

        Assert.False(result.Accepted);
        Assert.Equal("You can have at most 32 investment accounts.", result.Rejection);
        Assert.Equal(32, (await SyncedAsync(client, "investmentaccounts", ct)).Count);
    }

    [Fact]
    public async Task EnsuringATickerTwiceKeepsOneInstrumentAndCountsItOnce()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);

        await SendAsync(client, ct,
            new EnsureInstrument(Guid.NewGuid(), user, "PQR.PL", "Pqr SA"),
            new EnsureInstrument(Guid.NewGuid(), user, "pqr.pl", "Pqr SA again"),
            new SetQuoteSymbol(Guid.NewGuid(), user, "PQR.PL", "PQR.WA"));

        var instrument = Assert.Single(await SyncedAsync(client, "instruments", ct));
        Assert.Equal("Pqr SA", instrument.GetProperty("name").GetString());
        Assert.Equal("PQR.WA", instrument.GetProperty("quoteSymbol").GetString());
        var portfolio = Assert.Single(await SyncedAsync(client, "investmentportfolios", ct));
        Assert.Equal(["PQR.PL"], portfolio.GetProperty("_tickers").EnumerateArray().Select(ticker => ticker.GetString()));
    }

    [Fact]
    public async Task ADeletedOperationCanBeRestored()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, accountId) = await WithAnAccountAsync(factory, ct);
        var buy = ABuy(user, accountId);
        await SendAsync(client, ct, buy, new DeleteInvestmentOperation(Guid.NewGuid(), user, buy.OperationId));

        Assert.True(Assert.Single(await SyncedAsync(client, "investmentoperations", ct)).GetProperty("deleted").GetBoolean());

        await SendAsync(client, ct,
            new RestoreInvestmentOperation(Guid.NewGuid(), user, buy.OperationId),
            new EditInvestmentOperation(Guid.NewGuid(), user, buy.OperationId, OperationKind.Buy, TradedAt, "PQR.PL", 3m,
                60m, Pln(180m), null, "corrected"));

        var operation = Assert.Single(await SyncedAsync(client, "investmentoperations", ct));
        Assert.False(operation.GetProperty("deleted").GetBoolean());
        Assert.Equal(3m, operation.GetProperty("quantity").GetDecimal());
        Assert.Equal("corrected", operation.GetProperty("note").GetString());
    }
}
