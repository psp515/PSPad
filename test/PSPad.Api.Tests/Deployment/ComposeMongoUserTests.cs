using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Deployment;

[UnitTest]
public class ComposeMongoUserTests
{
    static readonly string Compose = RepositoryFiles.Read("docker", "compose.yaml");

    static string ApiConnectionString =>
        Compose.Split('\n').Single(line => line.TrimStart().StartsWith("Mongo__ConnectionString:", StringComparison.Ordinal));

    [Fact]
    public void TheApiSignsInAsItsOwnUserNotAsRoot()
    {
        Assert.Contains("${MONGO_APP_PASSWORD", ApiConnectionString);
        Assert.DoesNotContain("${MONGO_PASSWORD}", ApiConnectionString);
        Assert.DoesNotContain("authSource=admin", ApiConnectionString);
    }

    [Fact]
    public void TheApiStartsOnlyOnceItsUserExists()
    {
        Assert.Contains("mongo-app-user:\n        condition: service_completed_successfully", Compose);
    }
}
