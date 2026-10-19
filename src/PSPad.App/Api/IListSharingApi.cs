namespace PSPad.App.Api;

public interface IListSharingApi
{
    Task<SharingRejection?> ShareListAsync(Guid listId, string token, string code);

    Task<SharingRejection?> StopSharingAsync(Guid listId);

    Task<SharingRejection?> RemoveMemberAsync(Guid listId, Guid memberId);

    Task<SharingRejection?> LeaveListAsync(Guid listId);
}
