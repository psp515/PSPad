using System.Reflection;
using PSPad.Abstractions;

namespace PSPad.Contracts;

public static class CommandCatalogue
{
    static readonly Dictionary<string, Type> Commands = Build();

    public static Type? Resolve(string type) => Commands.GetValueOrDefault(type);

    public static IReadOnlyCollection<Type> All => Commands.Values;

    static Dictionary<string, Type> Build()
    {
        var module = Assembly.Load("PSPad.Module.Tasks");
        return module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } &&
                typeof(ICommand).IsAssignableFrom(type))
            .ToDictionary(type => type.Name, type => type);
    }
}
