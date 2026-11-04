using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PSPad.Api.Security;

namespace PSPad.Api.RateLimiting;

public static class RateLimiterRegistration
{
    public static IServiceCollection AddPSPadRateLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UserRateLimits>(configuration.GetSection(UserRateLimits.Section));

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                SecurityLog.For(context.HttpContext).RateLimited(
                    Subject(context.HttpContext.User) ?? "anonymous", context.HttpContext.Request.Path);

                return ValueTask.CompletedTask;
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                PerUser(context, Limits(context).Api));
            options.AddPolicy("commands", context => PerUser(context, Limits(context).Commands));
            options.AddPolicy("sync", context => PerUser(context, Limits(context).Sync));
            options.AddPolicy("statistics", context => PerUser(context, Limits(context).Statistics));
            options.AddPolicy("quotes", context => PerUser(context, Limits(context).Quotes));

            options.AddPolicy("public", context => RateLimitPartition.GetFixedWindowLimiter(
                ClientAddress(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue("Sharing:PublicRequestsPerMinute", 60),
                    Window = TimeSpan.FromMinutes(1)
                }));
            options.AddPolicy("join", context => RateLimitPartition.GetFixedWindowLimiter(
                Subject(context.User) ?? ClientAddress(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue("Sharing:JoinAttemptsPerWindow", 10),
                    Window = TimeSpan.FromMinutes(30)
                }));
        });
    }

    // Anonymous callers are left to the per-IP "public" policy; limiting them here would put
    // every visitor behind one shared bucket.
    static RateLimitPartition<string> PerUser(HttpContext context, TokenBucketLimits limits) =>
        Subject(context.User) is { } subject
            ? RateLimitPartition.GetTokenBucketLimiter(subject, _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = limits.TokenLimit,
                TokensPerPeriod = limits.TokensPerPeriod,
                ReplenishmentPeriod = limits.ReplenishmentPeriod,
                QueueLimit = 0,
                AutoReplenishment = true
            })
            : RateLimitPartition.GetNoLimiter("anonymous");

    static string ClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    static UserRateLimits Limits(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<UserRateLimits>>().Value;

    static string? Subject(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
}
