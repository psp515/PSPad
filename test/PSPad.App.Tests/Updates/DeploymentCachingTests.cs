using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Updates;

[UnitTest]
public class DeploymentCachingTests
{
    static readonly string Replica = File.ReadAllText(PathTo("src", "PSPad.App", "wwwroot", "js", "replica.js"));
    static readonly string Worker = File.ReadAllText(PathTo("src", "PSPad.App", "wwwroot", "service-worker.published.js"));
    static readonly string Nginx = File.ReadAllText(PathTo("docker", "nginx", "pspad.conf"));

    [Fact]
    public void AStaleScriptOpensTheNewerDatabaseInsteadOfFailing()
    {
        Assert.Contains("'VersionError'", Replica);
        Assert.Contains("indexedDB.open(DB_NAME)", Replica);
    }

    [Fact]
    public void AnOpenConnectionStepsAsideForAnUpgradeInAnotherTab()
    {
        Assert.Contains("db.onversionchange = () => forget(db)", Replica);
    }

    [Fact]
    public void EveryCallSharesOneOpenConnection()
    {
        Assert.Contains("connection ??= ", Replica);
    }

    [Fact]
    public void TheWorkerDoesNotPinTheIntegrityOfTheSettingsTheEntrypointRewrites()
    {
        Assert.Contains("const runtimeConfig = /appsettings\\.json$/;", Worker);
        Assert.Contains("runtimeConfig.test(asset.url) ? undefined : asset.hash", Worker);
    }

    [Fact]
    public void TheWorkerAnswersTheSettingsFromItsCacheAndRefreshesThemBehindTheBoot()
    {
        // A network that accepts the request and never answers would otherwise hold the boot forever.
        Assert.Contains("staleWhileRevalidate(event)", Worker);
        Assert.DoesNotContain("networkFirst", Worker);
    }

    [Fact]
    public void OnlyFingerprintedFrameworkFilesAreCachedAsImmutable()
    {
        var immutable = Nginx[..Nginx.IndexOf("immutable", StringComparison.Ordinal)];
        var block = immutable[immutable.LastIndexOf("location", StringComparison.Ordinal)..];

        Assert.Contains("/_framework/", block);
    }

    [Fact]
    public void UnfingerprintedScriptsAndStylesAreRevalidated()
    {
        Assert.Contains("location ~* \\.(?:js|css|json|webmanifest|png)$", Nginx);
    }

    static string PathTo(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return Path.Combine([directory!.FullName, .. parts]);
    }
}
