using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace PSPad.Api.Cors;

public static class CorsRegistration
{
    public const string Policy = "AppClient";

    public static IServiceCollection AddPSPadCors(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            options.AddPolicy(Policy, policy => policy
                .WithOrigins(allowedOrigins)
                .SetIsOriginAllowedToAllowWildcardSubdomains()
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders(HeaderNames.WWWAuthenticate));
        });

        return services;
    }
}
