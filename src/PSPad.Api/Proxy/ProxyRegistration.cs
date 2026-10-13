using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace PSPad.Api.Proxy;

public static class ProxyRegistration
{
    public static IServiceCollection AddPSPadForwardedHeaders(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>(Trust);
        return services;
    }

    static void Trust(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("ForwardedHeaders_Enabled"))
        {
            throw new InvalidOperationException(
                "ASPNETCORE_FORWARDEDHEADERS_ENABLED trusts forwarded headers from any caller. " +
                "Remove it and name the trusted proxy with Proxy__KnownNetworks__0 or Proxy__KnownProxies__0.");
        }

        var proxy = configuration.GetSection(ProxyOptions.Section).Get<ProxyOptions>() ?? new ProxyOptions();
        var networks = Filled(proxy.KnownNetworks).Select(System.Net.IPNetwork.Parse).ToList();
        var proxies = Filled(proxy.KnownProxies).Select(IPAddress.Parse).ToList();

        // The middleware believes any caller once both trust lists are empty.
        options.ForwardedHeaders = networks.Count + proxies.Count == 0
            ? ForwardedHeaders.None
            : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardedForHeaderName = proxy.ClientIpHeader;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        networks.ForEach(options.KnownIPNetworks.Add);
        proxies.ForEach(options.KnownProxies.Add);
    }

    static IEnumerable<string> Filled(IEnumerable<string> entries) =>
        entries.Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry.Trim());
}
