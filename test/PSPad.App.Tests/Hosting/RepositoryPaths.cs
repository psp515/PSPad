namespace PSPad.App.Tests.Hosting;

static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    public static string Wwwroot => Path.Combine(Root, "src", "PSPad.App", "wwwroot");

    public static string Nginx => Path.Combine(Root, "docker", "nginx");

    public static string ServingImage { get; } = File.ReadLines(Path.Combine(Root, "src", "PSPad.App", "Dockerfile"))
        .Last(line => line.StartsWith("FROM ", StringComparison.Ordinal))
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];

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
