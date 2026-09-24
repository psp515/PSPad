namespace PSPad.Api.Tests.Identity;

public sealed class FakeKeycloakAdminClient(bool succeeds, Exception? throws = null)
    : global::PSPad.Api.Identity.IKeycloakAdminClient
{
    public List<string> DeletedSubjects { get; } = [];

    public Task<bool> DeleteUserAsync(string subject, CancellationToken ct)
    {
        DeletedSubjects.Add(subject);

        if (throws is not null)
        {
            throw throws;
        }

        return Task.FromResult(succeeds);
    }
}
