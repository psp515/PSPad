namespace PSPad.Abstractions;

/// <summary>
/// Upper bounds on user-typed content, checked in every module's <c>Decide</c> and read by client inputs.
/// </summary>
public static class ContentLimits
{
    public const int Name = 64;
    public const int Label = 64;
    public const int FieldValue = 1_024;
    public const int Description = 8_192;
    public const int StepsPerTask = 128;
    public const int FieldsPerItem = 64;
    public const int MembersPerList = 100;

    /// <summary>Returns <paramref name="text"/>, or rejects it as <paramref name="what"/> when it is longer than <paramref name="limit"/>.</summary>
    public static string Within(string text, int limit, string what) =>
        text.Length > limit
            ? throw new DomainRejectedException($"{what} can be at most {limit} characters.")
            : text;

    /// <summary>Shortens <paramref name="text"/> to <paramref name="limit"/> characters.</summary>
    public static string Cut(string text, int limit) => text.Length > limit ? text[..limit] : text;
}
