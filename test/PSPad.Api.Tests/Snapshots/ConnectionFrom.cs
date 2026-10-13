using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace PSPad.Api.Tests.Snapshots;

public sealed class ConnectionFrom(IPAddress address) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress = address;
            return nextMiddleware(context);
        });
        next(app);
    };
}
