using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class PageHeadingAdoptionTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void MyDayNamesItself() => AssertTitle<Today>("My Day");

    [Fact]
    public void InboxNamesItself() => AssertTitle<InboxPage>("Inbox");

    [Fact]
    public void GoalsNamesItself() => AssertTitle<GoalsPage>("Goals");

    [Fact]
    public void AppInfoNamesItself() => AssertTitle<AppInfoPage>("App info");

    [Fact]
    public void NotFoundNamesItself() => AssertTitle<NotFound>("Not found");

    [Fact]
    public void AListScreenNamesItsListItsAreaAndLeadsBackToTheArea()
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Home", 0), DateTimeOffset.UnixEpoch));
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(null,
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), area.Id, "Shopping", ListKind.Tasks),
            DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12), area, list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        page.WaitForAssertion(() =>
        {
            var header = Services.GetRequiredService<PageHeader>();
            Assert.Equal("Shopping", header.Title);
            Assert.Equal("Home · no open tasks", header.Subtitle);
            Assert.Equal($"/areas/{area.Id}", header.BackHref);
        });
    }

    [Fact]
    public void AMissingListClearsAStaleHeader()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var header = Services.GetRequiredService<PageHeader>();
        header.Set("Stale title", "Stale subtitle", "/somewhere");

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, Guid.NewGuid()));

        page.WaitForAssertion(() =>
        {
            Assert.Equal("", header.Title);
            Assert.Null(header.BackHref);
        });
    }

    void AssertTitle<TPage>(string title) where TPage : Microsoft.AspNetCore.Components.IComponent
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));

        var page = Render<TPage>();

        page.WaitForAssertion(() =>
            Assert.Equal(title, Services.GetRequiredService<PageHeader>().Title));
    }
}
