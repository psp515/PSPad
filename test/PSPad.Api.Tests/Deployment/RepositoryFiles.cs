namespace PSPad.Api.Tests.Deployment;

static class RepositoryFiles
{
    public static string Root { get; } = FindRoot();

    public static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root, .. parts]));

    static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }
}
