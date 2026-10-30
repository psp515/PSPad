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
        }
    };
}
