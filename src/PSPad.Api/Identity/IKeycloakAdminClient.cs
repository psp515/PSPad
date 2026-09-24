namespace PSPad.Api.Identity;

public interface IKeycloakAdminClient
{
    Task<bool> DeleteUserAsync(string subject, CancellationToken ct);
}
