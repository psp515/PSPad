using System.Net;

namespace PSPad.App.Api;

public sealed class ServerBusyException(TimeSpan retryAfter)
    : HttpRequestException("The server asked to slow down.", null, HttpStatusCode.TooManyRequests)
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
