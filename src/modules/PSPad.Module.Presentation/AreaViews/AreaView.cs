using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Presentation.AreaViews;

public sealed class AreaView : Aggregate
{
    [JsonInclude]
    public Guid AreaId { get; private set; }

    [JsonInclude]
    public IReadOnlyList<Guid> Order { get; private set; } = [];

    public static Guid IdFor(Guid userId, Guid areaId)
    {
        // XOR rather than a hash: MD5 is unavailable in the browser, and the id only has to be stable and distinct.
        var user = userId.ToByteArray();
        var area = areaId.ToByteArray();
        var id = new byte[16];

        for (var index = 0; index < id.Length; index++)
        {
            id[index] = (byte)(user[index] ^ area[index]);
        }

        return new Guid(id);
    }

    public static IReadOnlyList<DomainEvent> Decide(AreaView? view, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case ReorderLists reorder:
                if (reorder.AreaId == Guid.Empty)
                {
                    throw new DomainRejectedException("Lists are ordered inside an area.");
                }

                if (view is not null && view.UserId != reorder.UserId)
                {
                    throw new DomainRejectedException("That area belongs to somebody else.");
                }

                if (reorder.Order.Distinct().Count() != reorder.Order.Count)
                {
                    throw new DomainRejectedException("A list appears twice in that order.");
                }

                if (!reorder.Order.Contains(reorder.ListId))
                {
                    throw new DomainRejectedException("The list being moved is not in this area.");
                }

                var order = reorder.Order.Where(id => id != reorder.ListId).ToList();
                order.Insert(Math.Clamp(reorder.ToIndex, 0, order.Count), reorder.ListId);

                return view is not null && view.Order.SequenceEqual(order)
                    ? []
                    : [new ListsReordered(
                        IdFor(reorder.UserId, reorder.AreaId), reorder.UserId, at, reorder.AreaId, order)];

            default:
                throw new DomainRejectedException($"An area view cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case ListsReordered reordered:
                Id = reordered.AggregateId;
                UserId = reordered.UserId;
                AreaId = reordered.AreaId;
                Order = reordered.Order;
                break;
        }
    }
}
