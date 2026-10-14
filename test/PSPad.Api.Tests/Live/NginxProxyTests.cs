using System.Net;
using System.Net.Sockets;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using PSPad.Api.Tests.Sync;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Live;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class NginxProxyTests(MongoFixture fixture)
{
    const string Image = "nginxinc/nginx-unprivileged:1.31.6-alpine";

    static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    static string StockConfig(int apiPort) => $$"""
        server {
            listen 8080;
            proxy_read_timeout 3s;
            location / {
                proxy_pass http://host.testcontainers.internal:{{apiPort}};
            }
        }
        """;

    async Task<(ApiFactory Factory, IContainer Nginx, Uri Proxy)> StartAsync(CancellationToken ct)
    {
        var apiPort = FreePort();
        var factory = new ApiFactory(fixture, configuration: new Dictionary<string, string?>
        {
            ["Live:Heartbeat"] = "00:00:01"
        });
        factory.UseKestrel(apiPort);
        factory.StartServer();
        await TestcontainersSettings.ExposeHostPortsAsync((ushort)apiPort, ct);

        var nginx = new ContainerBuilder(Image)
            .WithPortBinding(8080, true)
            .WithResourceMapping(Encoding.UTF8.GetBytes(StockConfig(apiPort)), "/etc/nginx/conf.d/default.conf")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath("/health")))
            .Build();
        await nginx.StartAsync(ct);

        return (factory, nginx, new Uri($"http://{nginx.Hostname}:{nginx.GetMappedPublicPort(8080)}"));
    }

    static HttpClient ThroughProxy(Uri proxy, string subject)
    {
        var client = new HttpClient { BaseAddress = proxy, Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Test-Zone", "Etc/UTC");
        return client;
    }

    [Fact]
    public async Task ANudgeCrossesStockNginxWithoutBuffering()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var (factory, nginx, proxy) = await StartAsync(ct);
        await using var _ = factory;
        await using var __ = nginx;
        var subject = Guid.NewGuid().ToString();
        using var client = ThroughProxy(proxy, subject);
        var userId = await Sharing.SignInAsync(client, ct);
        await using var live = await LiveListener.OpenAsync(client, ct);

        await Sharing.SendAsync(client, ct, new CreateArea(Guid.NewGuid(), userId, Guid.NewGuid(), "Dom", 0));

        Assert.Equal("sync", await live.NextAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task HeartbeatsKeepTheStreamOpenPastTheProxyReadTimeout()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var (factory, nginx, proxy) = await StartAsync(ct);
        await using var _ = factory;
        await using var __ = nginx;
        using var client = ThroughProxy(proxy, Guid.NewGuid().ToString());
        await Sharing.SignInAsync(client, ct);
        await using var live = await LiveListener.OpenAsync(client, ct);

        Assert.False(await live.EndsWithinAsync(TimeSpan.FromSeconds(7)));
    }
}
