namespace PSPad.App.Api;

public interface IListSharingApi
{
    Task<string?> ShareListAsync(Guid listId, string token, string code);

    Task<string?> StopSharingAsync(Guid listId);

    Task<string?> RemoveMemberAsync(Guid listId, Guid memberId);

    Task<string?> LeaveListAsync(Guid listId);
}
