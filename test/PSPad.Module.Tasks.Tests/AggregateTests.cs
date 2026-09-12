using PSPad.Abstractions;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests;

[UnitTest]
public class AggregateTests
{
    sealed record Renamed(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name)
        : DomainEvent(AggregateId, UserId, At);

    sealed class Thing : Aggregate
    {
        public string Name { get; private set; } = "";

        protected override void When(DomainEvent @event)
        {
            if (@event is Renamed renamed)
            {
                Id = renamed.AggregateId;
                UserId = renamed.UserId;
                Name = renamed.Name;
            }
        }
    }

    [Fact]
    public void ApplyFoldsTheEventAndAdvancesTheVersion()
    {
        var thing = new Thing();

        thing.Apply(new Renamed(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UnixEpoch, "groceries"));

        Assert.Equal("groceries", thing.Name);
        Assert.Equal(1, thing.Version);
    }
}
