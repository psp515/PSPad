namespace PSPad.Api.Tests.Identity;

public sealed class FakeKeycloakAdminClient(bool succeeds) : global::PSPad.Api.Identity.IKeycloakAdminClient
{
    public List<string> DeletedSubjects { get; } = [];

    public Task<bool> DeleteUserAsync(string subject, CancellationToken ct)
    {
        DeletedSubjects.Add(subject);
        return Task.FromResult(succeeds);
    }
}
