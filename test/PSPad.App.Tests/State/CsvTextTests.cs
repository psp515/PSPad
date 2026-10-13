using System.Text;
using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class CsvTextTests
{
    [Fact]
    public void Utf8WithABomIsRead()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("name\nŻabka")).ToArray();

        Assert.Equal("name\nŻabka", CsvText.Decode(bytes));
    }

    [Fact]
    public void InvalidUtf8FallsBackToWindows1250()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding(1250).GetBytes("name\nŻabka łódź");

        Assert.Equal("name\nŻabka łódź", CsvText.Decode(bytes));
    }
}
