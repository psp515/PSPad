using PSPad.App.State;
using PSPad.Module.Tasks.References;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class FieldDisplayTests
{
    [Theory]
    [InlineData("https://onedrive.live.com/x", FieldKind.Link)]
    [InlineData("http://printer.local", FieldKind.Link)]
    [InlineData(@"C:\Projects\Printer", FieldKind.Path)]
    [InlineData("C:/Projects/Printer", FieldKind.Path)]
    [InlineData(@"\\nas\print\benchy", FieldKind.Path)]
    [InlineData("/etc", FieldKind.Path)]
    [InlineData("~/models", FieldKind.Path)]
    [InlineData("350 g", FieldKind.Quantity)]
    [InlineData("350g", FieldKind.Quantity)]
    [InlineData("215 °C", FieldKind.Quantity)]
    [InlineData("0,4 mm", FieldKind.Quantity)]
    [InlineData("-5 %", FieldKind.Quantity)]
    [InlineData("3 pcs", FieldKind.Quantity)]
    [InlineData("black", FieldKind.Text)]
    [InlineData("PLA+ matte", FieldKind.Text)]
    [InlineData("350", FieldKind.Text)]
    [InlineData("", FieldKind.Text)]
    [InlineData("350 g and more", FieldKind.Text)]
    public void DetectsTheKindFromTheValue(string value, FieldKind expected) =>
        Assert.Equal(expected, FieldDisplay.Detect(value));

    [Fact]
    public void AStoredHintWins() =>
        Assert.Equal(FieldKind.Text, FieldDisplay.Of(new ReferenceField(Guid.NewGuid(), "Dir", "/etc", "text", 0)));

    [Fact]
    public void AnUnknownHintFallsBackToDetection() =>
        Assert.Equal(FieldKind.Path, FieldDisplay.Of(new ReferenceField(Guid.NewGuid(), "Dir", "/etc", "colour", 0)));

    [Fact]
    public void NoHintMeansDetect() => Assert.Null(FieldDisplay.HintFor(null));

    [Fact]
    public void AChosenKindBecomesItsHint() => Assert.Equal("path", FieldDisplay.HintFor(FieldKind.Path));
}
