using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Identity;

public sealed class User : Aggregate
{
    [JsonInclude]
    public string Subject { get; private set; } = "";

    [JsonInclude]
    public string DisplayName { get; private set; } = "";

    [JsonInclude]
    public string TimeZone { get; private set; } = "Etc/UTC";

    [JsonInclude]
    public string Language { get; private set; } = SupportedLanguages.Default;

    [JsonInclude]
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
                    provision.Subject, provision.DisplayName, RequireZone(provision.TimeZone),
                    SupportedLanguages.Require(provision.Language))];

            case SetUserTimeZone zone:
                var existing = Require(user);
                var id = RequireZone(zone.TimeZone);
                return existing.TimeZone == id ? [] : [new UserTimeZoneSet(existing.Id, existing.Id, at, id)];

            case SetUserDisplayName rename:
                var renaming = Require(user);
                var displayName = RequireDisplayName(rename.DisplayName);
                return renaming.DisplayName == displayName
                    ? []
                    : [new UserDisplayNameSet(renaming.Id, renaming.Id, at, displayName)];

            case SetUserLanguage language:
                var speaking = Require(user);
                var tag = SupportedLanguages.Require(language.Language);
                return speaking.Language == tag ? [] : [new UserLanguageSet(speaking.Id, speaking.Id, at, tag)];

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
                Language = provisioned.Language;
                ProvisionedAt = provisioned.At;
                break;
            case UserTimeZoneSet zoneSet:
                TimeZone = zoneSet.TimeZone;
                break;
            case UserLanguageSet languageSet:
                Language = languageSet.Language;
                break;
            case UserDisplayNameSet renamed:
                DisplayName = renamed.DisplayName;
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

    static string RequireDisplayName(string displayName) =>
        string.IsNullOrWhiteSpace(displayName)
            ? throw new DomainRejectedException("A user needs a display name.")
            : displayName.Trim();
}
