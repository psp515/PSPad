using PSPad.Abstractions;

namespace PSPad.Module.Tasks;

public static class ContentLimits
{
    public const int Name = 64;
    public const int Label = 64;
    public const int FieldValue = 1_024;
    public const int Description = 8_192;
    public const int StepsPerTask = 128;
    public const int FieldsPerItem = 64;
    public const int MembersPerList = 100;

    internal static string Within(string text, int limit, string what) =>
        text.Length > limit
            ? throw new DomainRejectedException($"{what} can be at most {limit} characters.")
            : text;

    internal static string Cut(string text, int limit) => text.Length > limit ? text[..limit] : text;
}
