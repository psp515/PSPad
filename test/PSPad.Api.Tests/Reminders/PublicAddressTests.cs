using System.Net;
using PSPad.Api.Reminders;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Reminders;

[UnitTest]
public class PublicAddressTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("64:ff9b::808:808")]
    [InlineData("2002:808:808::1")]
    public void APublicAddressIsAllowed(string address) =>
        Assert.True(PublicAddress.IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.20.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("239.255.255.250")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("198.18.0.1")]
    [InlineData("198.19.255.1")]
    [InlineData("192.0.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("192.88.99.1")]
    [InlineData("ff02::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2001::1")]
    [InlineData("100::1")]
    [InlineData("::10.0.0.1")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("64:ff9b:1::1")]
    [InlineData("2002:a00:1::1")]
    public void AnInternalAddressIsRefused(string address) =>
        Assert.False(PublicAddress.IsAllowed(IPAddress.Parse(address)));
}
