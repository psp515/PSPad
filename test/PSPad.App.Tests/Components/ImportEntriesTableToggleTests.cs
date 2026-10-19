using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.Module.Money.Import;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
[Collection("Culture")]
public class ImportEntriesTableToggleTests : Bunit.TestContext
{
    public ImportEntriesTableToggleTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    [Fact]
    public void TogglingARowCheckboxReportsItsLine()
    {
        var toggled = new List<int>();
        var rows = new[] { new EntryPreview(7, null, null, ImportStatus.ToImport, null) };
        var host = Render(builder =>
        {
            builder.OpenComponent<ImportEntriesTable>(0);
            builder.AddAttribute(1, nameof(ImportEntriesTable.Rows), (IReadOnlyList<EntryPreview>)rows);
            builder.AddAttribute(2, nameof(ImportEntriesTable.Toggled), (IReadOnlySet<int>)new HashSet<int>());
            builder.AddAttribute(3, nameof(ImportEntriesTable.OnToggle),
                EventCallback.Factory.Create<int>(this, toggled.Add));
            builder.CloseComponent();
        });

        host.WaitForElement("input[type=checkbox]").Change(false);

        host.WaitForAssertion(() => Assert.Equal([7], toggled), TimeSpan.FromSeconds(10));
    }
}
