using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Presentation.UserViews;

public sealed class UserView : Aggregate
{
    static readonly byte[] Salt = Guid.Parse("75736572-7669-6577-0000-000000000001").ToByteArray();

    [JsonInclude]
    public bool ShowFinishedOnSchedule { get; private set; }

    public static Guid IdFor(Guid userId)
    {
        var bytes = userId.ToByteArray();
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] ^= Salt[index];
        }

        return new Guid(bytes);
    }

    public static IReadOnlyList<DomainEvent> Decide(UserView? view, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case ShowFinishedOnSchedule show:
                if (view is not null && view.UserId != show.UserId)
                {
                    throw new DomainRejectedException("That view belongs to somebody else.");
                }

                return (view?.ShowFinishedOnSchedule ?? false) == show.Show
                    ? []
                    : [new FinishedOnScheduleShown(IdFor(show.UserId), show.UserId, at, show.Show)];

            default:
                throw new DomainRejectedException($"A user view cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        if (@event is FinishedOnScheduleShown shown)
        {
            Id = shown.AggregateId;
            UserId = shown.UserId;
            ShowFinishedOnSchedule = shown.Show;
        }
    }
}
