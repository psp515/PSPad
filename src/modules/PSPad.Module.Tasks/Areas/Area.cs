using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Areas;

public sealed class Area : Aggregate
{
    [JsonInclude]
    public string Name { get; private set; } = "";

    [JsonInclude]
    public int Position { get; private set; }

    public static IReadOnlyList<DomainEvent> Decide(Area? area, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case CreateArea create:
                if (area is not null)
                {
                    throw new DomainRejectedException("That area already exists.");
                }

                return [new AreaCreated(create.AreaId, create.UserId, at, RequireName(create.Name), create.Position)];

            case RenameArea rename:
                var renaming = Require(area, rename.UserId);
                var name = RequireName(rename.Name);
                return renaming.Name == name
                    ? []
                    : [new AreaRenamed(renaming.Id, rename.UserId, at, name)];

            case DeleteArea delete:
                var deleting = Require(area, delete.UserId);
                return deleting.Deleted ? [] : [new AreaDeleted(deleting.Id, delete.UserId, at)];

            default:
                throw new DomainRejectedException($"An area cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case AreaCreated created:
                Id = created.AggregateId;
                UserId = created.UserId;
                Name = created.Name;
                Position = created.Position;
                break;
            case AreaRenamed renamed:
                Name = renamed.Name;
                break;
            case AreaDeleted:
                Deleted = true;
                break;
        }
    }

    static Area Require(Area? area, Guid userId)
    {
        if (area is null || area.Deleted)
        {
            throw new DomainRejectedException("That area no longer exists.");
        }

        if (area.UserId != userId)
        {
            throw new DomainRejectedException("That area belongs to somebody else.");
        }

        return area;
    }

    static string RequireName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new DomainRejectedException("An area needs a name.")
            : name.Trim();
}
