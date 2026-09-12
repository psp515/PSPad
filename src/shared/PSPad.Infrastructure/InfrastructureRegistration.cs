using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddPSPadInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        MongoConventions.Register();

        services.AddOptions<MongoOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection(MongoOptions.Section).Bind(options);
            })
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    throw new InvalidOperationException("ConnectionStrings:Mongo is not configured.");
                }
            });

        services.AddSingleton<MongoContext>();
        services.AddScoped(typeof(IDocumentStore<>), typeof(MongoDocumentStore<>));
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
