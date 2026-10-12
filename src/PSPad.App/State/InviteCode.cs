using System.Security.Cryptography;
using PSPad.Abstractions;

namespace PSPad.App.State;

public static class InviteCode
{
    public static string New() =>
        string.Concat(Enumerable.Range(0, InviteCodes.Length)
            .Select(_ => InviteCodes.Alphabet[RandomNumberGenerator.GetInt32(InviteCodes.Alphabet.Length)]));

    public static string JoinLinkFor(string baseUri, string token, string code) =>
        $"{InviteToken.LinkFor(baseUri, token)}#code={code}";

    public static string BudgetJoinLinkFor(string baseUri, string token, string code) =>
        $"{InviteToken.BudgetLinkFor(baseUri, token)}#code={code}";
}
