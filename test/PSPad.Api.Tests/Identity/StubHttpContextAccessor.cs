using Microsoft.AspNetCore.Http;

namespace PSPad.Api.Tests.Identity;

public sealed class StubHttpContextAccessor(HttpContext httpContext) : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = httpContext;
}
