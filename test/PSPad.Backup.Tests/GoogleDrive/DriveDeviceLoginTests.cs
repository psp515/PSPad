using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using PSPad.Backup.GoogleDrive;
using PSPad.TestInfrastructure;

namespace PSPad.Backup.Tests.GoogleDrive;

[UnitTest]
public class DriveDeviceLoginTests
{
    const string Code = """{"device_code":"dev","user_code":"ABCD-EFGH","verification_url":"https://www.google.com/device","expires_in":1800,"interval":5}""";

    sealed class ScriptedGoogle(params (HttpStatusCode Status, string Body)[] replies) : HttpMessageHandler
    {
        readonly Queue<(HttpStatusCode Status, string Body)> _replies = new(replies);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (status, body) = _replies.Dequeue();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    static async Task<string> SignIn(ScriptedGoogle google, StringWriter prompt)
    {
        var clock = new FakeTimeProvider();
        var login = new DriveDeviceLogin(new HttpClient(google), clock, prompt).RefreshTokenAsync("id", "secret", TestContext.Current.CancellationToken);
        while (!login.IsCompleted)
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(1);
        }

        return await login;
    }

    [Fact]
    public async Task TheCodeIsShownAndPollingWaitsUntilTheUserApproves()
    {
        var prompt = new StringWriter();
        var google = new ScriptedGoogle(
            (HttpStatusCode.OK, Code),
            (HttpStatusCode.BadRequest, """{"error":"authorization_pending"}"""),
            (HttpStatusCode.OK, """{"access_token":"a","refresh_token":"refresh-me"}"""));

        Assert.Equal("refresh-me", await SignIn(google, prompt));
        Assert.Contains("ABCD-EFGH", prompt.ToString());
    }

    [Fact]
    public async Task ADeniedSignInIsReported()
    {
        var google = new ScriptedGoogle(
            (HttpStatusCode.OK, Code),
            (HttpStatusCode.BadRequest, """{"error":"access_denied"}"""));

        var failure = await Assert.ThrowsAsync<BackupFailure>(() => SignIn(google, new StringWriter()));

        Assert.Contains("access_denied", failure.Message);
    }

    [Fact]
    public async Task AClientOfTheWrongTypeIsExplained()
    {
        var google = new ScriptedGoogle((HttpStatusCode.Unauthorized, """{"error":"invalid_client"}"""));

        var failure = await Assert.ThrowsAsync<BackupFailure>(() => SignIn(google, new StringWriter()));

        Assert.Contains("TVs and Limited Input devices", failure.Message);
    }
}
