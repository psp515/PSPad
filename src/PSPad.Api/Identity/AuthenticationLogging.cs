using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PSPad.Api.Security;

namespace PSPad.Api.Identity;

public static class AuthenticationLogging
{
    public static JwtBearerEvents Events() => new()
    {
        OnAuthenticationFailed = context =>
        {
            SecurityLog.For(context.HttpContext).AuthenticationFailed(
                context.Request.Path, context.Exception.GetType().Name);
            return Task.CompletedTask;
        },
        OnForbidden = context =>
        {
            SecurityLog.For(context.HttpContext).Forbidden(
                Subject(context.HttpContext.User), context.Request.Path);
            return Task.CompletedTask;
        }
    };

    static string Subject(ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
}
