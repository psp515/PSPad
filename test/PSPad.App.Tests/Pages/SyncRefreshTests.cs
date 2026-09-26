using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.App.Pages;
using PSPad.App.State.Replica;
using PSPad.App.Tests;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class SyncRefreshTests : Bunit.TestContext
{
    const string SyncRevision = "SyncRevision";

    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    static readonly Type[] ReplicaBacked =
    [
        typeof(PSPad.App.Pages.Today), typeof(InboxPage), typeof(GoalsPage),
        typeof(AreaBoard), typeof(ListPage)
    ];

    static readonly Type[] NotReplicaBacked =
    [
        typeof(Authentication), typeof(Welcome), typeof(NotFound),
        typeof(SettingsPage), typeof(AppInfoPage),
        // The statistics screen reads the server's projected records over plain REST, not the replica.
        typeof(StatisticsPage),
        // Search renders what the typed query last matched, not a standing view of the replica.
        typeof(SearchPage)
    ];

    public static TheoryData<Type> ReplicaBackedPages()
    {
        var pages = new TheoryData<Type>();

        foreach (var page in ReplicaBacked)
        {
            pages.Add(page);
        }

        return pages;
    }

    [Theory]
    [MemberData(nameof(ReplicaBackedPages))]
    public void EveryReplicaBackedPageListensForTheSyncRevision(Type page)
    {
        var listens = page
            .GetProperties()
            .SelectMany(property =>
                property.GetCustomAttributes(typeof(CascadingParameterAttribute), true)
                    .Cast<CascadingParameterAttribute>())
            .Any(attribute => attribute.Name == SyncRevision);

        Assert.True(listens, $"{page.Name} renders replica data but never hears that sync refilled it.");
    }

    [Fact]
    public void EveryRoutablePageIsAccountedForOnOneSideOrTheOther()
    {
        // A new screen must make this choice deliberately rather than inherit stale data by default.
        var routable = typeof(App).Assembly.GetTypes()
            .Where(type => type.GetCustomAttributes(typeof(RouteAttribute), false).Length > 0);

        Assert.Empty(routable.Except(ReplicaBacked).Except(NotReplicaBacked));
    }

    [Fact]
    public async Task TodayShowsWhatSyncBroughtDownAfterItAlreadyRenderedAnEmptyReplica()
    {
        // Signing in wipes the replica, so the first render of My Day is always empty: the tasks
        // land moments later, on the first sync, with nothing to redraw the page.
        var replica = AppTestHost.Arrange(this, User, Today);
        var host = Render<RevisionHost<PSPad.App.Pages.Today>>(
            parameters => parameters.Add(host => host.Revision, 0));

        Assert.DoesNotContain("Buy milk", host.Markup);

        var list = NewList("Zakupy");
        await replica.SaveAsync(list);
        await replica.SaveAsync(Due(list.Id, "Buy milk", Today));

        host.Render(parameters => parameters.Add(host => host.Revision, 1));

        Assert.Contains("Buy milk", host.Markup);
    }

    [Fact]
    public async Task TheInboxShowsWhatSyncBroughtDownToo()
    {
        var replica = AppTestHost.Arrange(this, User, Today);
        var host = Render<RevisionHost<InboxPage>>(parameters => parameters.Add(host => host.Revision, 0));

        Assert.DoesNotContain("Call the dentist", host.Markup);

        await replica.SaveAsync(NewInbox("Call the dentist"));

        host.Render(parameters => parameters.Add(host => host.Revision, 1));

        Assert.Contains("Call the dentist", host.Markup);
    }

    [Fact]
    public void ARenderThatBringsNoNewSyncDoesNotSendThePageBackToTheReplica()
    {
        // AppShell re-renders on a theme change, a drawer toggle and every sync tick. Reloading on
        // each of those would put an IndexedDB round trip behind all of them.
        var replica = AppTestHost.Arrange(this, User, Today);
        var counting = new CountingReplica(replica);
        Services.AddSingleton<IDocumentStore<TodoTask>>(new ReplicaDocumentStore<TodoTask>(counting));
        Services.AddSingleton<IDocumentStore<TaskList>>(new ReplicaDocumentStore<TaskList>(counting));

        var host = Render<RevisionHost<PSPad.App.Pages.Today>>(
            parameters => parameters.Add(host => host.Revision, 3));
        var afterFirstRender = counting.Reads;

        host.Render(parameters => parameters.Add(host => host.Revision, 3));

        Assert.Equal(afterFirstRender, counting.Reads);
        Assert.NotEqual(0, afterFirstRender);
    }

    static TaskList NewList(string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name, 0),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static TodoTask Due(Guid listId, string name, DateOnly due)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), listId, name),
            DateTimeOffset.UnixEpoch));
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, due), DateTimeOffset.UnixEpoch));
        return task;
    }

    static Inbox NewInbox(string text)
    {
        var inbox = new Inbox();
        inbox.ApplyAll(Inbox.Decide(
            null, new CreateInbox(Guid.NewGuid(), User, Guid.NewGuid()), DateTimeOffset.UnixEpoch));
        inbox.ApplyAll(Inbox.Decide(
            inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), text),
            DateTimeOffset.UnixEpoch));
        return inbox;
    }

    // Cascading values cannot be re-supplied to an already-rendered component, so the page is
    // mounted under a host whose ordinary parameter feeds the cascade.
    public sealed class RevisionHost<TPage> : ComponentBase where TPage : IComponent
    {
        [Parameter] public int Revision { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<int>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<int>.Name), SyncRevision);
            builder.AddComponentParameter(2, nameof(CascadingValue<int>.Value), Revision);
            builder.AddComponentParameter(3, nameof(CascadingValue<int>.ChildContent),
                (RenderFragment)(child =>
                {
                    child.OpenComponent<TPage>(0);
                    child.CloseComponent();
                }));
            builder.CloseComponent();
        }
    }

    sealed class CountingReplica(IReplica inner) : IReplica
    {
        public int Reads { get; private set; }

        public Task<T?> LoadAsync<T>(Guid id) where T : Aggregate => inner.LoadAsync<T>(id);

        public Task<IReadOnlyList<T>> LoadAllAsync<T>(Guid userId) where T : Aggregate
        {
            Reads++;
            return inner.LoadAllAsync<T>(userId);
        }

        public Task SaveAsync(Aggregate aggregate) => inner.SaveAsync(aggregate);

        public Task<long> MarkerAsync() => inner.MarkerAsync();

        public Task SetMarkerAsync(long marker) => inner.SetMarkerAsync(marker);

        public Task<Guid?> OwnerAsync() => inner.OwnerAsync();

        public Task SetOwnerAsync(Guid userId) => inner.SetOwnerAsync(userId);

        public Task ClearAsync() => inner.ClearAsync();
    }
}
