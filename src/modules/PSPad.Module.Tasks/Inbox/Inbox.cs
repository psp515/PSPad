using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Tasks.Ordering;

namespace PSPad.Module.Tasks.Inbox;

public sealed class Inbox : Aggregate
{
    [JsonInclude]
    List<InboxItem> _items = [];

    public IReadOnlyList<InboxItem> Items => _items.OrderBy(item => item.Position).ToArray();

    public static IReadOnlyList<DomainEvent> Decide(Inbox? inbox, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case CreateInbox create:
                if (inbox is not null)
                {
                    throw new DomainRejectedException("That inbox already exists.");
                }

                return [new InboxCreated(create.InboxId, create.UserId, at)];

            case CaptureToInbox capture:
                var capturing = Require(inbox, capture.UserId);
                var text = RequireText(capture.Text);
                return capturing.Items.Any(item => item.Id == capture.ItemId)
                    ? []
                    : [new InboxItemCaptured(
                        capturing.Id, capture.UserId, at, capture.ItemId, text,
                        Positions.Next(capturing.Items.Select(item => item.Position)))];

            case OrganiseInboxItem organise:
                var organising = Require(inbox, organise.UserId);
                RequireItem(organising, organise.ItemId);
                return [new InboxItemOrganised(
                    organising.Id, organise.UserId, at, organise.ItemId, organise.TaskId, organise.ListId)];

            case DiscardInboxItem discard:
                var discarding = Require(inbox, discard.UserId);
                RequireItem(discarding, discard.ItemId);
                return [new InboxItemDiscarded(discarding.Id, discard.UserId, at, discard.ItemId)];

            default:
                throw new DomainRejectedException($"An inbox cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case InboxCreated created:
                Id = created.AggregateId;
                UserId = created.UserId;
                break;
            case InboxItemCaptured captured:
                _items.Add(new InboxItem(captured.ItemId, captured.Text, captured.At, captured.Position));
                break;
            case InboxItemOrganised organised:
                _items.RemoveAll(item => item.Id == organised.ItemId);
                Densify();
                break;
            case InboxItemDiscarded discarded:
                _items.RemoveAll(item => item.Id == discarded.ItemId);
                Densify();
                break;
        }
    }

    void Densify()
    {
        var ordered = _items.OrderBy(item => item.Position).ToArray();
        _items.Clear();
        for (var index = 0; index < ordered.Length; index++)
        {
            _items.Add(ordered[index] with { Position = index });
        }
    }

    static Inbox Require(Inbox? inbox, Guid userId)
    {
        if (inbox is null)
        {
            throw new DomainRejectedException("That inbox no longer exists.");
        }

        if (inbox.UserId != userId)
        {
            throw new DomainRejectedException("That inbox belongs to somebody else.");
        }

        return inbox;
    }

    static InboxItem RequireItem(Inbox inbox, Guid itemId) =>
        inbox.Items.FirstOrDefault(item => item.Id == itemId)
        ?? throw new DomainRejectedException("That item is no longer in your inbox.");

    static string RequireText(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? throw new DomainRejectedException("An inbox item needs some text.")
            : text.Trim();
}
