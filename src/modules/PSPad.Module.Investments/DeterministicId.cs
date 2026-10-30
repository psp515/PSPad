namespace PSPad.Module.Investments;

internal static class DeterministicId
{
    public static Guid From(Guid seed, ReadOnlySpan<byte> salt, ulong value)
    {
        var bytes = seed.ToByteArray();
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] ^= salt[index];
        }

        for (var index = 0; index < 8; index++)
        {
            bytes[8 + index] ^= (byte)(value >> (8 * index));
        }

        return new Guid(bytes);
    }

    public static ulong Fnv1a64(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var character in text)
        {
            hash = (hash ^ character) * 1099511628211UL;
        }

        return hash;
    }
}
