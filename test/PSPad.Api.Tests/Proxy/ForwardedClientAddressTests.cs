using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PSPad.Api.Proxy;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Proxy;

[UnitTest]
public class ForwardedClientAddressTests
{
    const string CloudflaredNetwork = "172.18.0.0/16";
    static readonly IPAddress Cloudflared = IPAddress.Parse("172.18.0.7");
    static readonly IPAddress Stranger = IPAddress.Parse("198.51.100.9");

    [Fact]
    public async Task ATrustedProxysCloudflareHeaderNamesTheVisitor()
    {
        var address = await ResolveAsync(
            Settings(("Proxy:KnownNetworks:0", CloudflaredNetwork), ("Proxy:ClientIpHeader", "CF-Connecting-IP")),
            Cloudflared,
            ("CF-Connecting-IP", "203.0.113.1"));

        Assert.Equal(IPAddress.Parse("203.0.113.1"), address);
    }

    [Fact]
    public async Task AnUntrustedCallerCannotNameItsOwnAddress()
    {
        var address = await ResolveAsync(
            Settings(("Proxy:KnownNetworks:0", CloudflaredNetwork), ("Proxy:ClientIpHeader", "CF-Connecting-IP")),
            Stranger,
            ("CF-Connecting-IP", "203.0.113.1"));

        Assert.Equal(Stranger, address);
    }

    [Fact]
    public async Task WithNoTrustedProxyForwardedHeadersAreIgnored()
    {
        var address = await ResolveAsync(Settings(), IPAddress.Loopback, ("X-Forwarded-For", "203.0.113.1"));

        Assert.Equal(IPAddress.Loopback, address);
    }

    [Fact]
    public async Task AKnownProxyAddressIsTrustedLikeANetwork()
    {
        var address = await ResolveAsync(
            Settings(("Proxy:KnownProxies:0", "172.18.0.7")), Cloudflared, ("X-Forwarded-For", "203.0.113.1"));

        Assert.Equal(IPAddress.Parse("203.0.113.1"), address);
    }

    [Fact]
    public async Task OnlyTheHopTheTrustedProxyAddedIsBelieved()
    {
        var address = await ResolveAsync(
            Settings(("Proxy:KnownNetworks:0", CloudflaredNetwork)),
            Cloudflared,
            ("X-Forwarded-For", "172.18.0.9, 203.0.113.1"));

        Assert.Equal(IPAddress.Parse("203.0.113.1"), address);
    }

    [Fact]
    public async Task ABlankNetworkTrustsNobody()
    {
        var address = await ResolveAsync(
            Settings(("Proxy:KnownNetworks:0", "")), Cloudflared, ("X-Forwarded-For", "203.0.113.1"));

        Assert.Equal(Cloudflared, address);
    }

    [Fact]
    public void AMalformedNetworkStopsStartup()
    {
        var configuration = Settings(("Proxy:KnownNetworks:0", "not-a-cidr"));

        Assert.Throws<FormatException>(() => OptionsFrom(configuration));
    }

    [Fact]
    public void TheTrustAnyProxySwitchStopsStartup()
    {
        var configuration = Settings(("ForwardedHeaders_Enabled", "true"));

        Assert.Throws<InvalidOperationException>(() => OptionsFrom(configuration));
    }

    static async Task<IPAddress?> ResolveAsync(
        IConfiguration configuration, IPAddress source, params (string Name, string Value)[] headers)
    {
        var options = Options.Create(OptionsFrom(configuration));
        IPAddress? seen = null;
        var middleware = new ForwardedHeadersMiddleware(
            context =>
            {
                seen = context.Connection.RemoteIpAddress;
                return Task.CompletedTask;
            },
            NullLoggerFactory.Instance,
            options);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = source;
        foreach (var (name, value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        await middleware.Invoke(context);

        return seen;
    }

    static ForwardedHeadersOptions OptionsFrom(IConfiguration configuration) =>
        new ServiceCollection()
            .AddSingleton(configuration)
            .AddPSPadForwardedHeaders()
            .BuildServiceProvider()
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

    static IConfiguration Settings(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();
}
