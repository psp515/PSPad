using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;

namespace PSPad.App.State.Dispatch;

public static class CommandRegistration
{
    public static IServiceCollection AddPSPadCommands(this IServiceCollection services)
    {
        var module = Assembly.Load("PSPad.Module.Tasks");

        foreach (var handler in module.GetTypes().Where(type => type is { IsAbstract: false, IsClass: true }))
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
