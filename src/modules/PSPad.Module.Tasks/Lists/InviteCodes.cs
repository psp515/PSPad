namespace PSPad.Module.Tasks.Lists;

public static class InviteCodes
{
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    public const int Length = 6;

    public static string Normalize(string? input) =>
        string.Concat((input ?? "").Where(character => character != '-' && !char.IsWhiteSpace(character)))
            .ToUpperInvariant();

    public static bool IsWellFormed(string code) => code.Length == Length && code.All(Alphabet.Contains);

    public static string Format(string code) => code.Length == Length ? $"{code[..3]}-{code[3..]}" : code;
}
