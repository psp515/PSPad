namespace PSPad.App.State;

public static class AvatarColor
{
    static readonly string[] Palette =
    [
        "#4E7A5E", "#3E6E7A", "#7A5E4E", "#5E4E7A",
        "#7A4E63", "#4E7A75", "#6E7A4E", "#7A6A4E"
    ];

    public static string For(Guid userId) =>
        Palette[(uint)userId.GetHashCode() % Palette.Length];

    public static string InitialOf(string email)
    {
        var trimmed = email.TrimStart();

        return trimmed.Length == 0 ? "?" : trimmed[..1].ToUpperInvariant();
    }
}
