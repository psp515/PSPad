using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class ReplicaScriptTests
{
    static readonly string Replica = File.ReadAllText(PathTo("replica.js"));
    static readonly string Session = File.ReadAllText(PathTo("session.js"));

    [Fact]
    public void ItBumpsTheDatabaseVersionForTheSessionStore()
    {
        Assert.Contains("const VERSION = 2;", Replica);
    }

    [Fact]
    public void ItCreatesTheSessionStore()
    {
        Assert.Contains("'session'", Replica);
    }

    [Fact]
    public void ItKeepsTheSessionOutOfTheClearedStores()
    {
        var clear = Replica[Replica.IndexOf("export function clearReplica", StringComparison.Ordinal)..];
        Assert.DoesNotContain("session", clear);
    }

    [Fact]
    public void ItKeepsTheOutboxOutOfTheClearedStores()
    {
        var clear = Replica[Replica.IndexOf("export function clearReplica", StringComparison.Ordinal)..];
        Assert.DoesNotContain("outbox", clear);
    }

    [Fact]
    public void ItSharesOneDatabaseOpener()
    {
        Assert.Contains("from './replica.js'", Session);
    }

    static string PathTo(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "src", "PSPad.App", "wwwroot", "js", file);
    }
}
