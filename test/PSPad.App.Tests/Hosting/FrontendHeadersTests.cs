using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Hosting;

[IntegrationTest]
public partial class FrontendHeadersTests(FrontendContainer frontend) : IClassFixture<FrontendContainer>
{
    public static TheoryData<string> Paths =>
        ["/", "/index.html", "/today", "/_framework/dotnet.native.wasm", "/css/app.css", "/manifest.webmanifest", "/missing.png"];

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task EveryResponseCarriesTheSecurityHeadersOnce(string path)
    {
        var response = await frontend.Client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("camera=(), microphone=(), geolocation=()", Assert.Single(response.Headers.GetValues("Permissions-Policy")));
    }

    [Fact]
    public async Task FingerprintedFrameworkFilesStayImmutable()
    {
        var response = await frontend.Client.GetAsync("/_framework/dotnet.native.wasm", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.CacheControl?.Extensions.Any(extension => extension.Name == "immutable"));
    }

    [Fact]
    public async Task ThePolicyAllowsEveryInlineScriptOfTheServedShellByHash()
    {
        var shell = await frontend.Client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var scriptSources = (await DirectivesAsync())["script-src"];

        var hashes = InlineScripts().Matches(shell).Select(match => Hash(match.Groups[1].Value)).ToList();

        Assert.Equal(5, hashes.Count);
        Assert.All(hashes, hash => Assert.Contains(hash, scriptSources));
        Assert.Equal(["'self'", "'wasm-unsafe-eval'", .. hashes], scriptSources);
    }

    [Fact]
    public async Task ThePolicyReachesOnlyTheConfiguredApiAndKeycloakOrigins()
    {
        var directives = await DirectivesAsync();

        Assert.Equal(["'self'"], directives["default-src"]);
        Assert.Equal(["'self'", "https://api.pad.example", "https://auth.pad.example:8443"], directives["connect-src"]);
        Assert.Equal(["'self'", "https://auth.pad.example:8443"], directives["frame-src"]);
        Assert.Equal(["'self'", "https://auth.pad.example:8443"], directives["form-action"]);
        Assert.Equal(["'self'", "'unsafe-inline'"], directives["style-src"]);
        Assert.Equal(["'self'", "data:"], directives["img-src"]);
        Assert.Equal(["'none'"], directives["object-src"]);
        Assert.Equal(["'self'"], directives["base-uri"]);
    }

    [Fact]
    public async Task OnlyTheAppItselfMayFrameThePageForTheSilentSignInCallback()
    {
        Assert.Equal(["'self'"], (await DirectivesAsync())["frame-ancestors"]);
    }

    [Fact]
    public async Task NginxRunsWithoutRoot()
    {
        var status = await frontend.ExecAsync(["cat", "/proc/1/status"]);
        var uid = status.Stdout.Split('\n').Single(line => line.StartsWith("Uid:", StringComparison.Ordinal))
            .Split('\t', StringSplitOptions.RemoveEmptyEntries)[1];

        Assert.NotEqual("0", uid);
    }

    async Task<Dictionary<string, string[]>> DirectivesAsync()
    {
        var response = await frontend.Client.GetAsync("/", TestContext.Current.CancellationToken);
        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));

        return policy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directive => directive.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(parts => parts[0], parts => parts[1..]);
    }

    static string Hash(string script) =>
        $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script.ReplaceLineEndings("\n"))))}'";

    [GeneratedRegex("""<script(?![^>]*\ssrc=)[^>]*>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex InlineScripts();
}
