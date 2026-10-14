using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PSPad.Api.Tests.Logging;

public static class RecordedLogsRegistration
{
    public static IServiceCollection AddRecordedLogs(this IServiceCollection services)
    {
        var logs = new RecordedLogs();
        services.AddLogging();
        services.AddSingleton(logs);
        services.AddSingleton<ILoggerProvider>(logs);
        return services;
    }
}
