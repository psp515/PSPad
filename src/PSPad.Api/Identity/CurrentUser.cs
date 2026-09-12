namespace PSPad.Api.Identity;

public interface ICurrentUser
{
    Guid UserId { get; }
}

public sealed class HeaderCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId =>
        Guid.TryParse(accessor.HttpContext?.Request.Headers["X-User-Id"], out var id)
            ? id
            : throw new InvalidOperationException("X-User-Id is missing.");
}
