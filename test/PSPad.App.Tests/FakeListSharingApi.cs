using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.Module.Tasks.Lists;

namespace PSPad.App.Tests;

public sealed class FakeListSharingApi(IReplica replica, AppState state, IClock clock) : IListSharingApi
{
    public List<string> Calls { get; } = [];

    public Task<SharingRejection?> ShareListAsync(Guid listId, string token, string code) =>
        ApplyAsync($"share {listId}", listId,
            new ShareTaskList(Guid.NewGuid(), state.UserId, listId, token, code, state.DisplayName));

    public Task<SharingRejection?> StopSharingAsync(Guid listId) =>
        ApplyAsync($"stop {listId}", listId, new StopSharingTaskList(Guid.NewGuid(), state.UserId, listId));

    public Task<SharingRejection?> RemoveMemberAsync(Guid listId, Guid memberId) =>
        ApplyAsync($"remove {memberId}", listId, new RemoveListMember(Guid.NewGuid(), state.UserId, listId, memberId));

    public Task<SharingRejection?> LeaveListAsync(Guid listId) =>
        ApplyAsync($"leave {listId}", listId, new LeaveTaskList(Guid.NewGuid(), state.UserId, listId));

    async Task<SharingRejection?> ApplyAsync(string call, Guid listId, ICommand command)
    {
        Calls.Add(call);
        var list = await replica.LoadAsync<TaskList>(listId);

        try
        {
            list!.ApplyAll(TaskList.Decide(list, command, clock.UtcNow));
            await replica.SaveAsync(list);
            return null;
        }
        catch (DomainRejectedException rejection)
        {
            return new SharingRejection(SharingFailure.Server, rejection.Message);
        }
    }
}
