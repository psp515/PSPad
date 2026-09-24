using System.Text.Json;
using PSPad.Abstractions;
using PSPad.Contracts;
using PSPad.Module.Tasks;

namespace PSPad.Module.Statistics;

public static class DomainEventCatalogue
{
    static readonly Dictionary<string, Type> Events = Build();

    public static Type? Resolve(string type) => Events.GetValueOrDefault(type);

    public static DomainEvent? Deserialize(RecordedEvent recorded)
    {
        var type = Resolve(recorded.Type);

        return type is null
            ? null
            : JsonSerializer.Deserialize(recorded.Payload, type) as DomainEvent;
    }

    static Dictionary<string, Type> Build() =>
        typeof(TasksModuleMarker).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } &&
                typeof(DomainEvent).IsAssignableFrom(type))
            .ToDictionary(type => type.Name, type => type);
}
