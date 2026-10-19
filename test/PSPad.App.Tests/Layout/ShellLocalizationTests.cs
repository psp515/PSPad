using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.App.Theme;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
[Collection("Culture")]
public class ShellLocalizationTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void TheSidebarSpeaksPolishUnderThePolishCulture()
    {
        var today = new DateOnly(2026, 9, 12);
        var replica = AppTestHost.Arrange(this, User, today);
        Services.AddSingleton(new ThemePreference(JSInterop.JSRuntime));
        Services.AddSingleton(new SidebarCounts(
            new ReplicaDocumentStore<Module.Tasks.Tasks.TodoTask>(replica),
            new ReplicaDocumentStore<Module.Tasks.Inbox.Inbox>(replica),
            new AppState { UserId = User, Today = today }));
        var before = CultureInfo.CurrentUICulture;
        var beforeFormat = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");

            var sidebar = Render<NavSidebar>(parameters => parameters.Add(p => p.UserId, User));

            Assert.Contains("Mój dzień", sidebar.Markup);
            Assert.Contains("Skrzynka", sidebar.Markup);
            Assert.DoesNotContain("My Day", sidebar.Markup);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
            CultureInfo.CurrentCulture = beforeFormat;
        }
    }
}
