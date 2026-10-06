using System.Security.Cryptography;
using PSPad.Module.Tasks.Lists;

namespace PSPad.App.State;

public static class InviteCode
{
    public static string New() =>
        string.Concat(Enumerable.Range(0, InviteCodes.Length)
            .Select(_ => InviteCodes.Alphabet[RandomNumberGenerator.GetInt32(InviteCodes.Alphabet.Length)]));

    public static string QrLinkFor(string baseUri, string token, string code) =>
        $"{InviteToken.LinkFor(baseUri, token)}#code={code}";
}
