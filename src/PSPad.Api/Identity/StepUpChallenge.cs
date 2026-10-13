using System.Globalization;

namespace PSPad.Api.Identity;

public sealed class StepUpChallenge(TimeSpan maxAge) : IResult
{
    public const string Error = "insufficient_user_authentication";

    public Task ExecuteAsync(HttpContext httpContext)
    {
        var seconds = ((long)maxAge.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        httpContext.Response.Headers.WWWAuthenticate =
            $"Bearer error=\"{Error}\", error_description=\"Sign in again to continue.\", max_age={seconds}";
        return Task.CompletedTask;
    }
}
