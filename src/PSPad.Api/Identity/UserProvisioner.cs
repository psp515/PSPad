using PSPad.Abstractions;
using PSPad.Module.Identity;
using PSPad.Module.Identity.Provisioning;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;

namespace PSPad.Api.Identity;

public sealed class UserProvisioner(
    IDocumentStore<User> users,
    IDocumentStore<Area> areas,
    IDocumentStore<Inbox> inboxes,
    IUnitOfWork work,
    IClock clock)
{
    readonly IDocumentStore<Area> _areas = areas;
    readonly IDocumentStore<Inbox> _inboxes = inboxes;

    public async Task<User> EnsureAsync(
        string subject, string displayName, string timeZone, CancellationToken ct)
    {
        var id = User.IdFor(subject);
        var existing = await users.LoadAsync(id, ct);

        if (existing?.ProvisionedAt is not null)
        {
            return existing;
        }

        var commandId = Guid.NewGuid();
        var at = clock.UtcNow;

        var user = new User();
        var userEvents = User.Decide(
            existing, new ProvisionUser(commandId, id, subject, displayName, timeZone), at);
        user.ApplyAll(userEvents);
        work.Stage(user, userEvents);

        var inbox = new Inbox();
        var inboxEvents = Inbox.Decide(null, new CreateInbox(commandId, id, Guid.NewGuid()), at);
        inbox.ApplyAll(inboxEvents);
        work.Stage(inbox, inboxEvents);

        for (var position = 0; position < ProvisioningPlan.SeedAreaNames.Count; position++)
        {
            var area = new Area();
            var areaEvents = Area.Decide(
                null,
                new CreateArea(commandId, id, Guid.NewGuid(), ProvisioningPlan.SeedAreaNames[position], position),
                at);
            area.ApplyAll(areaEvents);
            work.Stage(area, areaEvents);
        }

        await work.CommitAsync(commandId, id, ct);
        return user;
    }
}
