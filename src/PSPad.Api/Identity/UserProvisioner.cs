using PSPad.Abstractions;
using PSPad.Api.Commands;
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

    public Task<User> EnsureAsync(
        string subject, string displayName, string timeZone, string language, CancellationToken ct) =>
        ConflictRetry.RunAsync(() => ProvisionAsync(subject, displayName, timeZone, language, ct), ct);

    async Task<User> ProvisionAsync(
        string subject, string displayName, string timeZone, string language, CancellationToken ct)
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
            existing, new ProvisionUser(commandId, id, subject, displayName, timeZone, language), at);
        user.ApplyAll(userEvents);
        work.Stage(user, userEvents);

        var inbox = new Inbox();
        var inboxEvents = Inbox.Decide(null, new CreateInbox(commandId, id, Guid.NewGuid()), at);
        inbox.ApplyAll(inboxEvents);
        work.Stage(inbox, inboxEvents);

        var seeds = ProvisioningPlan.SeedAreaNames(language);
        for (var position = 0; position < seeds.Count; position++)
        {
            var area = new Area();
            var areaEvents = Area.Decide(
                null,
                new CreateArea(commandId, id, Guid.NewGuid(), seeds[position], position),
                at);
            area.ApplyAll(areaEvents);
            work.Stage(area, areaEvents);
        }

        await work.CommitAsync(commandId, id, ct);
        return user;
    }

    public Task<User> SetTimeZoneAsync(User user, string timeZone, CancellationToken ct) =>
        DecideAndCommitAsync(
            user.Id, fresh => new SetUserTimeZone(Guid.NewGuid(), fresh.Id, timeZone), ct);

    public Task<User> SetLanguageAsync(User user, string language, CancellationToken ct) =>
        DecideAndCommitAsync(
            user.Id, fresh => new SetUserLanguage(Guid.NewGuid(), fresh.Id, language), ct);

    public Task<User> RenameAsync(User user, string displayName, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(displayName) || user.DisplayName == displayName
            ? Task.FromResult(user)
            : DecideAndCommitAsync(
                user.Id, fresh => new SetUserDisplayName(Guid.NewGuid(), fresh.Id, displayName), ct);

    Task<User> DecideAndCommitAsync(Guid userId, Func<User, ICommand> commandFor, CancellationToken ct) =>
        ConflictRetry.RunAsync(async () =>
        {
            var user = await users.LoadAsync(userId, ct)
                ?? throw new InvalidOperationException($"User {userId} is not provisioned.");
            var command = commandFor(user);
            var events = User.Decide(user, command, clock.UtcNow);

            if (events.Count == 0)
            {
                return user;
            }

            user.ApplyAll(events);
            work.Stage(user, events);
            await work.CommitAsync(command.CommandId, user.Id, ct);

            return user;
        }, ct);
}
