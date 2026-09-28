using System.Text.RegularExpressions;
using PSPad.Module.Tasks.References;

namespace PSPad.App.State;

public static partial class FieldDisplay
{
    public static FieldKind Detect(string value)
    {
        var trimmed = value.Trim();

        if (LinkPattern().IsMatch(trimmed))
        {
            return FieldKind.Link;
        }

        if (PathPattern().IsMatch(trimmed))
        {
            return FieldKind.Path;
        }

        return QuantityPattern().IsMatch(trimmed) ? FieldKind.Quantity : FieldKind.Text;
    }

    public static FieldKind Of(ReferenceField field) =>
        field.Display is { } hint && !int.TryParse(hint, out _) &&
        Enum.TryParse<FieldKind>(hint, ignoreCase: true, out var kind)
            ? kind
            : Detect(field.Value);

    public static string? HintFor(FieldKind? chosen) => chosen?.ToString().ToLowerInvariant();

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"^([A-Za-z]:[\\/]|\\\\|/|~/)")]
    private static partial Regex PathPattern();

    [GeneratedRegex(@"^-?\d+([.,]\d+)?\s*[^\d\s]\S*$")]
    private static partial Regex QuantityPattern();
}
