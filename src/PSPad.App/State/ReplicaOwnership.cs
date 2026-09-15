namespace PSPad.App.State;

public sealed class ReplicaOwnership(IReplica replica, IOutbox outbox)
{
    public async Task EnsureCurrentUserAsync(Guid userId)
    {
        var owner = await replica.OwnerAsync();

        if (owner is not null && owner != userId)
        {
            await replica.ClearAsync();
            await outbox.ClearAsync();
        }

        await replica.SetOwnerAsync(userId);
    }
}
