using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using PSPad.App.Pages;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class RoutablePageAuthorizationTests
{
    static readonly Type[] PublicByDesign = [typeof(Authentication), typeof(NotFound)];

    static readonly Type[] Guarded = typeof(App).Assembly.GetTypes()
        .Where(type => type.GetCustomAttributes(typeof(RouteAttribute), false).Length > 0)
        .Where(type => !PublicByDesign.Contains(type))
        .ToArray();

    public static TheoryData<Type> RoutablePages()
    {
        var pages = new TheoryData<Type>();

        foreach (var page in Guarded)
        {
            pages.Add(page);
        }

        return pages;
    }

    [Theory]
    [MemberData(nameof(RoutablePages))]
    public void EveryRoutablePageRequiresAnAuthenticatedUser(Type page)
    {
        Assert.NotEmpty(page.GetCustomAttributes(typeof(AuthorizeAttribute), true));
    }

    [Fact]
    public void TheAuthenticationRouteStaysReachableWithoutASession()
    {
        Assert.Empty(typeof(Authentication).GetCustomAttributes(typeof(AuthorizeAttribute), true));
    }

    [Fact]
    public void ItFindsTheRoutablePagesItClaimsToGuard()
    {
        Assert.Contains(typeof(Today), Guarded);
        Assert.True(Guarded.Length >= 8);
    }
}
