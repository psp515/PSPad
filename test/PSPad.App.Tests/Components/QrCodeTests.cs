using Bunit;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class QrCodeTests : Bunit.TestContext
{
    public QrCodeTests() => Services.AddMudServices();

    [Fact]
    public void ItRendersAPngOfTheValue()
    {
        var qr = Render<QrCode>(parameters => parameters
            .Add(code => code.Value, "https://pspad.home/join/abc")
            .Add(code => code.Size, 176)
            .Add(code => code.Label, "QR code for the invite link"));

        var image = qr.Find("img.pspad-qr");
        Assert.StartsWith("data:image/png;base64,", image.GetAttribute("src"));
        Assert.Equal("176", image.GetAttribute("width"));
        Assert.Equal("QR code for the invite link", image.GetAttribute("alt"));
    }

    [Fact]
    public void ADifferentValueGivesADifferentImage()
    {
        string Source(string value) => Render<QrCode>(parameters => parameters
            .Add(code => code.Value, value)).Find("img").GetAttribute("src")!;

        Assert.NotEqual(Source("a"), Source("b"));
    }
}
