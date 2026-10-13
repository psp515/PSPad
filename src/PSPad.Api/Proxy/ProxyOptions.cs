namespace PSPad.Api.Proxy;

public sealed class ProxyOptions
{
    public const string Section = "Proxy";

    public string[] KnownNetworks { get; set; } = [];

    public string[] KnownProxies { get; set; } = [];

    public string ClientIpHeader { get; set; } = "X-Forwarded-For";
}
