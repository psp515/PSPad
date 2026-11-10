using PSPad.Abstractions;
using PSPad.Module.Presentation.UserViews;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Presentation.Tests.UserViews;

[UnitTest]
public class ShowFinishedOnScheduleHandlerTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TurningItOffWithNoViewStagesNothing()
    {
        var work = new RecordingUnitOfWork();
        var handler = new ShowFinishedOnScheduleHandler(new EmptyStore(), work, new FixedClock());

        var result = await handler.HandleAsync(new ShowFinishedOnSchedule(Guid.NewGuid(), User, false), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Empty(work.Staged);
    }

    [Fact]
    public async Task TurningItOnStagesTheView()
    {
        var work = new RecordingUnitOfWork();
        var handler = new ShowFinishedOnScheduleHandler(new EmptyStore(), work, new FixedClock());

        var result = await handler.HandleAsync(new ShowFinishedOnSchedule(Guid.NewGuid(), User, true), CancellationToken.None);

        Assert.True(result.Accepted);
        var view = Assert.IsType<UserView>(Assert.Single(work.Staged));
        Assert.Equal(UserView.IdFor(User), view.Id);
        Assert.True(view.ShowFinishedOnSchedule);
    }

    sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    sealed class EmptyStore : IDocumentStore<UserView>
    {
        public Task<UserView?> LoadAsync(Guid id, CancellationToken ct) => Task.FromResult<UserView?>(null);

        public Task<IReadOnlyList<UserView>> LoadAllAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UserView>>([]);
    }

    sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public List<Aggregate> Staged { get; } = [];

        public void Stage(Aggregate aggregate, IReadOnlyList<DomainEvent> events) => Staged.Add(aggregate);

        public Task CommitAsync(Guid commandId, Guid userId, CancellationToken ct) => Task.CompletedTask;

        public Task<bool> IsProcessedAsync(Guid commandId, CancellationToken ct) => Task.FromResult(false);
    }
}
