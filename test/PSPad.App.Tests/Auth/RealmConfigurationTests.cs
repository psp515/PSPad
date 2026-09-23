using System.Text.Json;
using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class RealmConfigurationTests
{
    static readonly JsonDocument Realm = JsonDocument.Parse(File.ReadAllText(PathToRealm()));

    [Fact]
    public void ItKeepsTheServerSessionLongerThanTheLocalTrustWindow()
    {
        var idle = Realm.RootElement.GetProperty("ssoSessionIdleTimeout").GetInt32();

        Assert.True(TimeSpan.FromSeconds(idle) > SessionBootstrapper.TrustWindow);
    }

    [Fact]
    public void ItCapsTheSessionLifespan()
    {
        Assert.Equal(7776000, Realm.RootElement.GetProperty("ssoSessionMaxLifespan").GetInt32());
    }

    static string PathToRealm()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docker")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "docker", "keycloak", "realm-psplace.json");
    }
}
