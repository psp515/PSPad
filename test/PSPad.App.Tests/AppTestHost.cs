using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App.State;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.State.Viewport;
using PSPad.App.Theme;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.Tests;

public static class AppTestHost
{
    public static InMemoryReplica Arrange(
        Bunit.TestContext context, Guid userId, DateOnly today, params Aggregate[] documents) =>
        Arrange(context, userId, today, isDesktop: true, documents);

    public static InMemoryReplica Arrange(
        Bunit.TestContext context, Guid userId, DateOnly today, bool isDesktop, params Aggregate[] documents)
    {
        context.JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        context.Services.Options = new ServiceProviderOptions { ValidateScopes = false };
        context.Services.AddMudServices();

        var replica = new InMemoryReplica();
        replica.SetOwnerAsync(userId).GetAwaiter().GetResult();
        foreach (var document in documents)
        {
            replica.SaveAsync(document).GetAwaiter().GetResult();
        }

        var outbox = new InMemoryOutbox();
        var work = new ReplicaUnitOfWork(replica, outbox);

        context.Services.AddSingleton<IReplica>(replica);
        context.Services.AddSingleton<IOutbox>(outbox);
        context.Services.AddSingleton(work);
        context.Services.AddSingleton<IUnitOfWork>(work);
        context.Services.AddSingleton<IClock>(new FixedClock(today));
        context.Services.AddSingleton<IDocumentStore<Area>>(new ReplicaDocumentStore<Area>(replica));
        context.Services.AddSingleton<IDocumentStore<TaskList>>(new ReplicaDocumentStore<TaskList>(replica));
        context.Services.AddSingleton<IDocumentStore<TodoTask>>(new ReplicaDocumentStore<TodoTask>(replica));
        context.Services.AddSingleton<IDocumentStore<Goal>>(new ReplicaDocumentStore<Goal>(replica));
        context.Services.AddSingleton<IDocumentStore<Inbox>>(new ReplicaDocumentStore<Inbox>(replica));
        context.Services.AddPSPadCommands();
        context.Services.AddSingleton(new AppState { UserId = userId, Today = today });
        context.Services.AddSingleton(new ThemePreference(context.JSInterop.JSRuntime));

        var collapse = new CardCollapseState(context.JSInterop.JSRuntime);
        collapse.LoadAsync().GetAwaiter().GetResult();
        context.Services.AddSingleton(collapse);

        context.Services.AddSingleton(services => new CommandSender(services, work));
        context.Services.AddSingleton(new ReplicaOwnership(replica, outbox));
        context.Services.AddSingleton<IViewport>(new FakeViewport(isDesktop));

        return replica;
    }

    sealed class FixedClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow => new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    public sealed class FakeViewport(bool isDesktop) : IViewport
    {
        public Task SubscribeAsync(Action<bool> onDesktopChanged)
        {
            onDesktopChanged(isDesktop);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
