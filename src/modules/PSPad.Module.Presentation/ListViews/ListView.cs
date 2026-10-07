using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Presentation.AreaViews;

namespace PSPad.Module.Presentation.ListViews;

public sealed class ListView : Aggregate
{
    [JsonInclude]
    public Guid ListId { get; private set; }

    [JsonInclude]
    public Guid? AreaId { get; private set; }

    public static Guid IdFor(Guid userId, Guid listId) => AreaView.IdFor(userId, listId);

    public static IReadOnlyList<DomainEvent> Decide(ListView? view, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case PlaceList place:
                if (place.ListId == Guid.Empty)
                {
                    throw new DomainRejectedException("Pick a list to file.");
                }

                if (view is not null && view.UserId != place.UserId)
                {
                    throw new DomainRejectedException("That list view belongs to somebody else.");
                }

                return view is not null && view.AreaId == place.AreaId
                    ? []
                    : [new ListPlaced(IdFor(place.UserId, place.ListId), place.UserId, at, place.ListId, place.AreaId)];

            default:
                throw new DomainRejectedException($"A list view cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        if (@event is ListPlaced placed)
        {
            Id = placed.AggregateId;
            UserId = placed.UserId;
            ListId = placed.ListId;
            AreaId = placed.AreaId;
        }
    }
}
