using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PSPad.Api.Security;
using PSPad.Api.Tests.Logging;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Security;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class SecurityLoggingTests(MongoFixture fixture)
{
    [Fact]
    public async Task AWrongInviteCodeIsLoggedWithoutTheCodeOrToken()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, testServices: services => services.AddRecordedLogs());
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);

        await Sharing.JoinAsync(member, ct, token, "QQQQQQ");

        var entry = Assert.Single(Security(factory), record => record.Id.Id == SecurityLog.WrongInviteCodeId);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(memberId.ToString(), entry.Message);
        Assert.Contains($"list {listId}", entry.Message);
        Assert.DoesNotContain("QQQQQQ", entry.Message);
        Assert.DoesNotContain(token, entry.Message);
    }

    [Fact]
    public async Task AWrongBudgetInviteCodeIsLoggedWithoutTheCodeOrToken()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, testServices: services => services.AddRecordedLogs());
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);
        var token = Sharing.FreshToken();
        var budgetId = await BudgetSharing.SharedBudgetAsync(owner, ownerId, ct, token);

        await BudgetSharing.JoinAsync(member, ct, token, "QQQQQQ");

        var entry = Assert.Single(Security(factory), record => record.Id.Id == SecurityLog.WrongInviteCodeId);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(memberId.ToString(), entry.Message);
        Assert.Contains($"budget {budgetId}", entry.Message);
        Assert.DoesNotContain("QQQQQQ", entry.Message);
        Assert.DoesNotContain(token, entry.Message);
    }

    [Fact]
    public async Task ARateLimitedRequestIsLoggedWithTheSubjectAndPath()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(
            fixture,
            configuration: new Dictionary<string, string?>
            {
                ["RateLimits:Commands:TokenLimit"] = "1",
                ["RateLimits:Commands:TokensPerPeriod"] = "1",
                ["RateLimits:Commands:ReplenishmentPeriod"] = "01:00:00"
            },
            testServices: services => services.AddRecordedLogs());
        var subject = Guid.NewGuid().ToString();
        var client = factory.ClientFor(subject);

        await client.PostAsJsonAsync("/api/commands", Array.Empty<CommandEnvelope>(), ct);
        var refused = await client.PostAsJsonAsync("/api/commands", Array.Empty<CommandEnvelope>(), ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        var entry = Assert.Single(Security(factory), record => record.Id.Id == SecurityLog.RateLimitedId);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(subject, entry.Message);
        Assert.Contains("/api/commands", entry.Message);
    }

    [Fact]
    public async Task AGarbageBearerTokenIsLogged()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, testServices: services =>
        {
            services.AddRecordedLogs();
            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            });
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Configuration = new OpenIdConnectConfiguration();
                options.ConfigurationManager = null;
            });
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync("/api/me", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var entry = Assert.Single(Security(factory), record => record.Id.Id == SecurityLog.AuthenticationFailedId);
        Assert.Contains("/api/me", entry.Message);
        Assert.DoesNotContain("not-a-jwt", entry.Message);
    }

    static IEnumerable<LogEntry> Security(ApiFactory factory) =>
        factory.Services.GetRequiredService<RecordedLogs>().Entries
            .Where(record => record.Level >= LogLevel.Warning);
}
