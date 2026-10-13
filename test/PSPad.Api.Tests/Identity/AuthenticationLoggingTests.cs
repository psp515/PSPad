using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PSPad.Api.Identity;
using PSPad.Api.Security;
using PSPad.Api.Tests.Logging;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Identity;

[UnitTest]
public class AuthenticationLoggingTests
{
    static readonly AuthenticationScheme Scheme =
        new(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));

    readonly ServiceProvider _services = new ServiceCollection().AddRecordedLogs().BuildServiceProvider();

    [Fact]
    public async Task ARejectedTokenIsLoggedWithThePathAndFailureKind()
    {
        var http = Request("/api/sync");
        var context = new AuthenticationFailedContext(http, Scheme, new JwtBearerOptions())
        {
            Exception = new SecurityTokenExpiredException("token eyJsecret expired")
        };

        await AuthenticationLogging.Events().AuthenticationFailed(context);

        var entry = Assert.Single(Logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(SecurityLog.AuthenticationFailedId, entry.Id.Id);
        Assert.Contains("/api/sync", entry.Message);
        Assert.Contains(nameof(SecurityTokenExpiredException), entry.Message);
        Assert.DoesNotContain("eyJsecret", entry.Message);
    }

    [Fact]
    public async Task AForbiddenRequestIsLoggedWithTheSubjectAndPath()
    {
        var http = Request("/api/lists/1/share");
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "kc-subject")], "jwt"));

        await AuthenticationLogging.Events().Forbidden(new ForbiddenContext(http, Scheme, new JwtBearerOptions()));

        var entry = Assert.Single(Logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(SecurityLog.ForbiddenId, entry.Id.Id);
        Assert.Contains("kc-subject", entry.Message);
        Assert.Contains("/api/lists/1/share", entry.Message);
    }

    RecordedLogs Logs => _services.GetRequiredService<RecordedLogs>();

    DefaultHttpContext Request(string path) =>
        new() { RequestServices = _services, Request = { Path = path } };
}
