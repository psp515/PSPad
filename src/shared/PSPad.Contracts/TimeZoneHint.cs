namespace PSPad.Contracts;

/// <summary>The request header that carries the browser's IANA time zone, used once to set a new account's zone.</summary>
public static class TimeZoneHint
{
    public const string Header = "X-Time-Zone";
}
