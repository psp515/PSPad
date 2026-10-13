using System.Text.RegularExpressions;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Deployment;

[UnitTest]
public partial class ContainerImageTests
{
    static readonly string ApiDockerfile = RepositoryFiles.Read("src", "PSPad.Api", "Dockerfile");
    static readonly string AppDockerfile = RepositoryFiles.Read("src", "PSPad.App", "Dockerfile");
    static readonly string Compose = RepositoryFiles.Read("docker", "compose.yaml");

    public static TheoryData<string> PulledImages =>
        [.. BaseImages(ApiDockerfile), .. BaseImages(AppDockerfile), .. ComposeImages()];

    [Theory]
    [MemberData(nameof(PulledImages))]
    public void EveryPulledImageIsPinnedToAnExactVersionTag(string image)
    {
        Assert.Matches(PinnedImage(), image);
    }

    [Fact]
    public void TheApiDropsRootBeforeItStarts()
    {
        var runtime = FinalStage(ApiDockerfile);

        Assert.Contains("USER $APP_UID", runtime);
        Assert.True(runtime.IndexOf("USER $APP_UID", StringComparison.Ordinal) < runtime.IndexOf("ENTRYPOINT", StringComparison.Ordinal));
    }

    [Fact]
    public void TheClientIsServedByNginxWithoutRoot()
    {
        Assert.StartsWith("nginxinc/nginx-unprivileged:", BaseImages(AppDockerfile).Last());
        Assert.DoesNotContain("USER root", FinalStage(AppDockerfile));
    }

    [Fact]
    public void TheBundledKeycloakIsTheReleaseProductionRuns()
    {
        Assert.Contains("quay.io/keycloak/keycloak:26.7.4", ComposeImages());
    }

    static IEnumerable<string> BaseImages(string dockerfile) =>
        FromLine().Matches(dockerfile).Select(match => match.Groups[1].Value);

    static IEnumerable<string> ComposeImages() =>
        ImageLine().Matches(Compose).Select(match => match.Groups[1].Value).Where(image => !image.EndsWith(":local", StringComparison.Ordinal));

    static string FinalStage(string dockerfile) => dockerfile[dockerfile.LastIndexOf("\nFROM ", StringComparison.Ordinal)..];

    [GeneratedRegex(@"^FROM\s+(\S+)", RegexOptions.Multiline)]
    private static partial Regex FromLine();

    [GeneratedRegex(@"^\s+image:\s+(\S+)", RegexOptions.Multiline)]
    private static partial Regex ImageLine();

    [GeneratedRegex(@"^[^:@\s]+:\d+\.\d+\.\d+[^:@\s]*$")]
    private static partial Regex PinnedImage();
}
