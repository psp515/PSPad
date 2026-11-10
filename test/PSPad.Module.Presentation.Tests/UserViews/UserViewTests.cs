using PSPad.Abstractions;
using PSPad.Module.Presentation.UserViews;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Presentation.Tests.UserViews;

[UnitTest]
public class UserViewTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IdForIsStablePerUserAndNeverTheUserId()
    {
        Assert.Equal(UserView.IdFor(User), UserView.IdFor(User));
        Assert.NotEqual(UserView.IdFor(User), UserView.IdFor(Guid.NewGuid()));
        Assert.NotEqual(User, UserView.IdFor(User));
    }

    [Fact]
    public void FinishedTasksLeaveTheScheduleUntilTurnedOn()
    {
        Assert.False(new UserView().ShowFinishedOnSchedule);
    }

    [Fact]
    public void TurningItOnCreatesTheView()
    {
        var shown = Assert.IsType<FinishedOnScheduleShown>(Assert.Single(
            UserView.Decide(null, new ShowFinishedOnSchedule(Guid.NewGuid(), User, true), Now)));

        var view = new UserView();
        view.ApplyAll([shown]);

        Assert.Equal(UserView.IdFor(User), view.Id);
        Assert.Equal(User, view.UserId);
        Assert.True(view.ShowFinishedOnSchedule);
    }

    [Fact]
    public void SettingTheSameValueAgainChangesNothing()
    {
        var view = new UserView();
        view.ApplyAll(UserView.Decide(null, new ShowFinishedOnSchedule(Guid.NewGuid(), User, true), Now));

        Assert.Empty(UserView.Decide(view, new ShowFinishedOnSchedule(Guid.NewGuid(), User, true), Now));
    }

    [Fact]
    public void TurningItOffAgainRestoresTheDefault()
    {
        var view = new UserView();
        view.ApplyAll(UserView.Decide(null, new ShowFinishedOnSchedule(Guid.NewGuid(), User, true), Now));
        view.ApplyAll(UserView.Decide(view, new ShowFinishedOnSchedule(Guid.NewGuid(), User, false), Now));

        Assert.False(view.ShowFinishedOnSchedule);
    }

    [Fact]
    public void TurningItOffWithNoViewChangesNothing()
    {
        Assert.Empty(UserView.Decide(null, new ShowFinishedOnSchedule(Guid.NewGuid(), User, false), Now));
    }

    [Fact]
    public void AnotherUsersViewIsRejected()
    {
        var view = new UserView();
        view.ApplyAll(UserView.Decide(null, new ShowFinishedOnSchedule(Guid.NewGuid(), User, true), Now));

        Assert.Throws<DomainRejectedException>(
            () => UserView.Decide(view, new ShowFinishedOnSchedule(Guid.NewGuid(), Guid.NewGuid(), false), Now));
    }
}
