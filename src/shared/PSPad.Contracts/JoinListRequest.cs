namespace PSPad.Contracts;

public sealed record JoinListRequest(string Token, string Code = "");
