using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;
using PSPad.Module.Investments.Portfolio;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class InvestmentSyncTests(MongoFixture fixture)
{
    [Fact]
    public async Task ThePortfolioSyncsWithTheAccountsItCounts()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var accountId = Guid.NewGuid();

        await SendAsync(client, ct,
            new CreateInvestmentAccount(Guid.NewGuid(), user, accountId, "IKE", "USD", Broker.Xtb, "12345678"),
            new EnsureInstrument(Guid.NewGuid(), user, "FUND.UK", "Index Fund"));

        var portfolio = Assert.Single(await SyncedAsync(client, "investmentportfolios", ct));
        Assert.Equal(InvestmentPortfolio.IdFor(user), portfolio.GetProperty("id").GetGuid());
        Assert.Equal([accountId], portfolio.GetProperty("_accountIds").EnumerateArray().Select(id => id.GetGuid()));
        var account = Assert.Single(await SyncedAsync(client, "investmentaccounts", ct));
        Assert.Equal("USD", account.GetProperty("currency").GetString());
        Assert.Equal("12345678", account.GetProperty("brokerAccountNumber").GetString());
        var instrument = Assert.Single(await SyncedAsync(client, "instruments", ct));
        Assert.Equal(Instrument.IdFor(user, "FUND.UK"), instrument.GetProperty("id").GetGuid());
        Assert.Equal("FUND.L", instrument.GetProperty("quoteSymbol").GetString());
    }

    [Fact]
    public async Task AnotherUserSeesNoneOfMyInvestments()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(owner, ct);
        await SendAsync(owner, ct,
            new CreateInvestmentAccount(Guid.NewGuid(), user, Guid.NewGuid(), "IKE", "PLN", Broker.Xtb, null),
            new EnsureInstrument(Guid.NewGuid(), user, "PQR.PL", "Pqr SA"));
        var other = factory.ClientFor(Guid.NewGuid().ToString());
        await SignInAsync(other, ct);

        foreach (var collection in new[] { "investmentportfolios", "investmentaccounts", "instruments" })
        {
            Assert.Empty(await SyncedAsync(other, collection, ct));
        }
    }
}
