using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

public sealed class ApiFactory(MongoFixture fixture) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = fixture.ConnectionString,
                ["Mongo:Database"] = "pspad_test"
            }));

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthenticationHandler.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.Scheme, _ => { });
        });

    public HttpClient ClientFor(string subject, string zone = "Etc/UTC")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Test-Zone", zone);
        return client;
    }
}
