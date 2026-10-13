using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace PSPad.App.Tests.Hosting;

public sealed class FrontendContainer : IAsyncLifetime
{
    public const string ApiBaseAddress = "https://api.pad.example/";
    public const string KeycloakAuthority = "https://auth.pad.example:8443/realms/psplace";

    const string Html = "/usr/share/nginx/html";
    const UnixFileModes Executable = UnixFileModes.UserRead | UnixFileModes.UserWrite | UnixFileModes.UserExecute
        | UnixFileModes.GroupRead | UnixFileModes.GroupExecute | UnixFileModes.OtherRead | UnixFileModes.OtherExecute;

    public static string PublishedIndex { get; } = File.ReadAllText(Path.Combine(RepositoryPaths.Wwwroot, "index.html"))
        .Replace("<script type=\"importmap\"></script>",
            "<script type=\"importmap\">{\r\n  \"imports\": {\r\n    \"./_framework/dotnet.js\": \"./_framework/dotnet.abc123.js\"\r\n  }\r\n}</script>",
            StringComparison.Ordinal);

    readonly IContainer _container = new ContainerBuilder("nginx:alpine")
        .WithResourceMapping(Nginx("pspad.conf"), "/etc/nginx/conf.d/default.conf")
        .WithResourceMapping(Nginx("security-headers.inc.template"), "/etc/nginx/templates/security-headers.inc.template")
        .WithResourceMapping(Nginx("19-pspad-csp.envsh"), "/docker-entrypoint.d/19-pspad-csp.envsh", fileMode: Executable)
        .WithResourceMapping(System.Text.Encoding.UTF8.GetBytes(PublishedIndex), $"{Html}/index.html")
        .WithResourceMapping("body {}"u8.ToArray(), $"{Html}/css/app.css")
        .WithResourceMapping("{}"u8.ToArray(), $"{Html}/manifest.webmanifest")
        .WithResourceMapping(new byte[] { 0, 97, 115, 109 }, $"{Html}/_framework/dotnet.native.wasm")
        .WithEnvironment("API_BASE_ADDRESS", ApiBaseAddress)
        .WithEnvironment("KEYCLOAK_AUTHORITY", KeycloakAuthority)
        .WithPortBinding(8080, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath("/")))
        .Build();

    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        Client = new HttpClient { BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(8080)}") };
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _container.DisposeAsync();
    }

    static byte[] Nginx(string file) => File.ReadAllBytes(Path.Combine(RepositoryPaths.Nginx, file));
}
