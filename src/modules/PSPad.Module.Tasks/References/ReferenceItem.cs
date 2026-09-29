using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Tasks.Ordering;

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
    public DateTimeOffset? CreatedAt { get; private set; }

    [JsonInclude]
    List<ReferenceField> _fields = [];

    public IReadOnlyList<ReferenceField> Fields => _fields.OrderBy(entry => entry.Position).ToArray();

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
                var description = (describe.Description ?? "").TrimEnd();
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

            case AddReferenceField add:
                var adding = Require(item, add.UserId);
                return adding.Fields.Any(field => field.Id == add.FieldId)
                    ? []
                    : [new ReferenceFieldAdded(
                        adding.Id, add.UserId, at, add.FieldId, RequireLabel(add.Label), (add.Value ?? "").TrimEnd(), Hint(add.Display),
                        Positions.Next(adding.Fields.Select(field => field.Position)))];

            case EditReferenceField edit:
                var editing = Require(item, edit.UserId);
                var existing = RequireField(editing, edit.FieldId);
                var label = RequireLabel(edit.Label);
                var value = (edit.Value ?? "").TrimEnd();
                var hint = Hint(edit.Display);
                return existing.Label == label && existing.Value == value && existing.Display == hint
                    ? []
                    : [new ReferenceFieldEdited(editing.Id, edit.UserId, at, edit.FieldId, label, value, hint)];

            case MoveReferenceField moveField:
                var reordering = Require(item, moveField.UserId);
                RequireField(reordering, moveField.FieldId);
                var order = Positions.Move(
                    reordering.Fields.Select(field => field.Id).ToArray(), moveField.FieldId, moveField.ToIndex);
                return [new ReferenceFieldsReordered(reordering.Id, moveField.UserId, at, order)];

            case RemoveReferenceField remove:
                var removing = Require(item, remove.UserId);
                RequireField(removing, remove.FieldId);
                return [new ReferenceFieldRemoved(removing.Id, remove.UserId, at, remove.FieldId)];

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
                CreatedAt = created.At;
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
            case ReferenceFieldAdded added:
                _fields.Add(new ReferenceField(added.FieldId, added.Label, added.Value, added.Display, added.Position));
                break;
            case ReferenceFieldEdited edited:
                Replace(edited.FieldId, field => field with { Label = edited.Label, Value = edited.Value, Display = edited.Display });
                break;
            case ReferenceFieldsReordered reordered:
                for (var index = 0; index < reordered.Order.Count; index++)
                {
                    Replace(reordered.Order[index], field => field with { Position = index });
                }

                break;
            case ReferenceFieldRemoved removed:
                _fields.RemoveAll(field => field.Id == removed.FieldId);
                Densify();
                break;
        }
    }

    void Replace(Guid fieldId, Func<ReferenceField, ReferenceField> change)
    {
        var index = _fields.FindIndex(field => field.Id == fieldId);
        if (index >= 0)
        {
            _fields[index] = change(_fields[index]);
        }
    }

    void Densify()
    {
        var ordered = _fields.OrderBy(field => field.Position).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            Replace(ordered[index].Id, field => field with { Position = index });
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

    static string RequireLabel(string label) =>
        string.IsNullOrWhiteSpace(label)
            ? throw new DomainRejectedException("A field needs a label.")
            : label.Trim();

    static string? Hint(string? display) => string.IsNullOrWhiteSpace(display) ? null : display.Trim();

    static ReferenceField RequireField(ReferenceItem item, Guid fieldId) =>
        item.Fields.FirstOrDefault(field => field.Id == fieldId)
        ?? throw new DomainRejectedException("That field is not on this item.");
}
