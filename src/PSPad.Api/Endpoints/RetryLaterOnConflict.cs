using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Endpoints;

public sealed class RetryLaterOnConflict : IEndpointFilter
{
    public const int RetryAfterSeconds = 1;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ConcurrencyConflictException)
        {
            context.HttpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString();
            return Results.Problem(
                "Someone else changed this at the same time. Try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
