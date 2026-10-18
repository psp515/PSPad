namespace PSPad.Abstractions;

public static class SupportedLanguages
{
    public const string Default = "en";

    public static IReadOnlyList<string> All { get; } = ["en", "pl"];

    public static bool IsSupported(string? tag) =>
        tag is not null && All.Contains(tag, StringComparer.Ordinal);

    public static string Require(string? tag) =>
        IsSupported(tag) ? tag! : throw new DomainRejectedException($"{tag} is not a language this app speaks.");

    public static string FromBrowser(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return Default;
        }

        var primary = tag.Split('-', ',', ';')[0].Trim().ToLowerInvariant();
        return IsSupported(primary) ? primary : Default;
    }
}
