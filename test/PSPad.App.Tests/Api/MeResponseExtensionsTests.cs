using PSPad.App.Api;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Api;

[UnitTest]
public class MeResponseExtensionsTests
{
    [Fact]
    public void ANullDisplayNameOrEmailBecomesEmpty()
    {
        var me = new MeResponse(Guid.NewGuid(), null!, null!, "UTC");

        var sanitized = me.Sanitized()!;

        Assert.Equal("", sanitized.DisplayName);
        Assert.Equal("", sanitized.Email);
    }

    [Fact]
    public void ANullOrEmptyTimeZoneFallsBackToUtc()
    {
        var withNull = new MeResponse(Guid.NewGuid(), "Ada", "ada@example.com", null!);
        var withEmpty = new MeResponse(Guid.NewGuid(), "Ada", "ada@example.com", "");

        Assert.Equal("Etc/UTC", withNull.Sanitized()!.TimeZone);
        Assert.Equal("Etc/UTC", withEmpty.Sanitized()!.TimeZone);
    }

    [Fact]
    public void AnEmptyUserIdIsNoAccountAtAll()
    {
        var me = new MeResponse(Guid.Empty, "Ada", "ada@example.com", "UTC");

        Assert.Null(me.Sanitized());
    }

    [Fact]
    public void AMissingResponseIsNoAccountAtAll()
    {
        Assert.Null(((MeResponse?)null).Sanitized());
    }

    [Fact]
    public void ValidFieldsPassThroughUnchanged()
    {
        var me = new MeResponse(Guid.NewGuid(), "Ada Lovelace", "ada@example.com", "Europe/Warsaw");

        var sanitized = me.Sanitized()!;

        Assert.Equal(me.UserId, sanitized.UserId);
        Assert.Equal("Ada Lovelace", sanitized.DisplayName);
        Assert.Equal("ada@example.com", sanitized.Email);
        Assert.Equal("Europe/Warsaw", sanitized.TimeZone);
    }
}
