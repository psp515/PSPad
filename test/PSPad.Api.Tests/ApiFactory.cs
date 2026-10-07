using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PSPad.Api.Identity;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

public sealed class ApiFactory(
    MongoFixture fixture,
    IKeycloakAdminClient? keycloakAdminClient = null,
    IReadOnlyDictionary<string, string?>? configuration = null)
    : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configurationBuilder =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = fixture.ConnectionString,
                ["Mongo:Database"] = "pspad_test"
            };

            foreach (var (key, value) in configuration ?? new Dictionary<string, string?>())
            {
                settings[key] = value;
            }

            configurationBuilder.AddInMemoryCollection(settings);
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthenticationHandler.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.Scheme, _ => { });

            if (keycloakAdminClient is not null)
            {
                services.AddSingleton(keycloakAdminClient);
            }
        });

    public HttpClient ClientFor(
        string subject, string zone = "Etc/UTC", string? name = null, string? email = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Test-Zone", zone);

        if (name is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Name", name);
        }

        if (email is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Email", email);
        }

        return client;
    }
}
