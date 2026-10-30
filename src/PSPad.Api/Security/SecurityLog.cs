using Microsoft.Extensions.Logging;

namespace PSPad.Api.Security;

public static partial class SecurityLog
{
    public const int CommandRefusedId = 4001;
    public const int AuthenticationFailedId = 4002;
    public const int WrongInviteCodeId = 4004;
    public const int RateLimitedId = 4005;

    const int LongestLoggedName = 100;

    public static ILogger For(HttpContext context) =>
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SecurityLog).FullName!);

    [LoggerMessage(EventId = CommandRefusedId, Level = LogLevel.Warning,
        Message = "Command {CommandType} from user {UserId} refused: {Reason}")]
    static partial void LogCommandRefused(ILogger logger, string commandType, Guid userId, string reason);

    public static void CommandRefused(this ILogger logger, string commandType, Guid userId, string reason) =>
        LogCommandRefused(logger, Bounded(commandType), userId, reason);

    [LoggerMessage(EventId = AuthenticationFailedId, Level = LogLevel.Warning,
        Message = "Bearer token rejected on {Path}: {Failure}")]
    public static partial void AuthenticationFailed(this ILogger logger, string path, string failure);

    [LoggerMessage(EventId = WrongInviteCodeId, Level = LogLevel.Warning,
        Message = "User {UserId} gave a wrong invite code for {Shared} {SharedId}")]
    public static partial void WrongInviteCode(this ILogger logger, Guid userId, string shared, Guid sharedId);

    [LoggerMessage(EventId = RateLimitedId, Level = LogLevel.Warning,
        Message = "User {Subject} rate limited on {Path}")]
    public static partial void RateLimited(this ILogger logger, string subject, string path);

    static string Bounded(string value) =>
        value.Length <= LongestLoggedName ? value : value[..LongestLoggedName] + "…";
}
