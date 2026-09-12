using System.Security.Cryptography;
using System.Text;
using PSPad.Abstractions;

namespace PSPad.Module.Identity;

public sealed class User : Aggregate
{
    public string Subject { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string TimeZone { get; private set; } = "Etc/UTC";
    public DateTimeOffset? ProvisionedAt { get; private set; }

    public static Guid IdFor(string subject) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes(subject)));

    public static IReadOnlyList<DomainEvent> Decide(User? user, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case ProvisionUser provision:
                if (user?.ProvisionedAt is not null)
                {
                    return [];
                }

                return [new UserProvisioned(
                    IdFor(provision.Subject), IdFor(provision.Subject), at,
                    provision.Subject, provision.DisplayName, RequireZone(provision.TimeZone))];

            case SetUserTimeZone zone:
                var existing = Require(user);
                var id = RequireZone(zone.TimeZone);
                return existing.TimeZone == id ? [] : [new UserTimeZoneSet(existing.Id, existing.Id, at, id)];

            default:
                throw new DomainRejectedException($"A user cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case UserProvisioned provisioned:
                Id = provisioned.AggregateId;
                UserId = provisioned.UserId;
                Subject = provisioned.Subject;
                DisplayName = provisioned.DisplayName;
                TimeZone = provisioned.TimeZone;
                ProvisionedAt = provisioned.At;
                break;
            case UserTimeZoneSet zoneSet:
                TimeZone = zoneSet.TimeZone;
                break;
        }
    }

    static User Require(User? user) =>
        user ?? throw new DomainRejectedException("That user does not exist.");

    static string RequireZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id).Id;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new DomainRejectedException($"{id} is not a time zone this system knows.");
        }
    }
}
