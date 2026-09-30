using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Contracts;

namespace PSPad.App.State.Dispatch;

public static class CommandRegistration
{
    public static IServiceCollection AddPSPadCommands(this IServiceCollection services)
    {
        var handlers = CommandModules.Names
            .SelectMany(name => Assembly.Load(name).GetTypes())
            .Where(type => type is { IsAbstract: false, IsClass: true });

        foreach (var handler in handlers)
        {
            foreach (var contract in handler.GetInterfaces()
                .Where(@interface => @interface.IsGenericType &&
                    @interface.GetGenericTypeDefinition() == typeof(ICommandHandler<>)))
            {
                services.AddScoped(contract, handler);
            }
        }

        return services;
    }
}
