using PSPad.Api.Live;

namespace PSPad.Api.Endpoints;

public static class LiveEndpoints
{
    public static void MapLiveEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("live", LiveStream.RunAsync);
}
