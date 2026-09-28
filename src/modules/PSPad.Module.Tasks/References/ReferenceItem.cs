using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed class ReferenceItem : Aggregate
{
    [JsonInclude]
    public Guid ListId { get; private set; }

    [JsonInclude]
    public string Name { get; private set; } = "";

    [JsonInclude]
    public string Description { get; private set; } = "";

    [JsonInclude]
    public bool Starred { get; private set; }

    [JsonInclude]
    public int Position { get; private set; }

    [JsonInclude]
    List<ReferenceField> _fields = [];

    public IReadOnlyList<ReferenceField> Fields => _fields.OrderBy(item => item.Position).ToArray();

    public static IReadOnlyList<DomainEvent> Decide(ReferenceItem? item, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case CreateReferenceItem create:
                if (item is not null)
                {
                    throw new DomainRejectedException("That item already exists.");
                }

                if (create.ListId == Guid.Empty)
                {
                    throw new DomainRejectedException("An item has to live in a list.");
                }

                return [new ReferenceItemCreated(create.ItemId, create.UserId, at, create.ListId, RequireName(create.Name), create.Position)];

            case RenameReferenceItem rename:
                var renaming = Require(item, rename.UserId);
                var name = RequireName(rename.Name);
                return renaming.Name == name ? [] : [new ReferenceItemRenamed(renaming.Id, rename.UserId, at, name)];

            case SetReferenceItemDescription describe:
                var describing = Require(item, describe.UserId);
                var description = describe.Description.TrimEnd();
                return describing.Description == description
                    ? []
                    : [new ReferenceItemDescriptionSet(describing.Id, describe.UserId, at, description)];

            case StarReferenceItem star:
                var starring = Require(item, star.UserId);
                return starring.Starred == star.Starred ? [] : [new ReferenceItemStarred(starring.Id, star.UserId, at, star.Starred)];

            case MoveReferenceItemToList move:
                var moving = Require(item, move.UserId);
                if (move.ListId == Guid.Empty)
                {
                    throw new DomainRejectedException("An item has to live in a list.");
                }

                return moving.ListId == move.ListId ? [] : [new ReferenceItemMovedToList(moving.Id, move.UserId, at, move.ListId)];

            case DeleteReferenceItem delete:
                var deleting = Require(item, delete.UserId);
                return [new ReferenceItemDeleted(deleting.Id, delete.UserId, at, deleting.Name)];

            default:
                throw new DomainRejectedException($"A reference item cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case ReferenceItemCreated created:
                Id = created.AggregateId;
                UserId = created.UserId;
                ListId = created.ListId;
                Name = created.Name;
                Position = created.Position;
                break;
            case ReferenceItemRenamed renamed:
                Name = renamed.Name;
                break;
            case ReferenceItemDescriptionSet described:
                Description = described.Description;
                break;
            case ReferenceItemStarred starred:
                Starred = starred.Starred;
                break;
            case ReferenceItemMovedToList moved:
                ListId = moved.ListId;
                break;
            case ReferenceItemDeleted:
                Deleted = true;
                break;
        }
    }

    internal static ReferenceItem Require(ReferenceItem? item, Guid userId)
    {
        if (item is null || item.Deleted)
        {
            throw new DomainRejectedException("That item no longer exists.");
        }

        if (item.UserId != userId)
        {
            throw new DomainRejectedException("That item belongs to somebody else.");
        }

        return item;
    }

    static string RequireName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new DomainRejectedException("An item needs a name.")
            : name.Trim();
}
