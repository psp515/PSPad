using PSPad.TestInfrastructure;
using Xunit.Sdk;

namespace PSPad.App.Tests;

[UnitTest]
public sealed class EndToEndLocatorGuardTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), $"e2e-guard-{Guid.NewGuid():N}");

    public EndToEndLocatorGuardTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "scenarios"));
        Directory.CreateDirectory(Path.Combine(_root, "helpers"));
        Write("scenarios/01-clean.spec.ts", "await page.getByRole('button', { name: 'Save' }).click();");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void EndToEndStepsLocateByRoleLabelOrText() =>
        EndToEndLocatorGuard.AssertLocatorsAreAccessible();

    [Fact]
    public void RoleLabelAndTextLocatorsPass() =>
        EndToEndLocatorGuard.AssertLocatorsAreAccessible(_root);

    [Theory]
    [InlineData("page.locator('.pspad-row')")]
    [InlineData("page.locator(\".pspad-row\")")]
    [InlineData("page.locator(`.pspad-${kind}`)")]
    [InlineData("page.locator('.pspad-' + kind)")]
    [InlineData("pickRow(page, 'pspad-task-goal')")]
    [InlineData("page.locator('[class*=pspad-row]')")]
    [InlineData("await row.getAttribute('class')")]
    [InlineData("page.locator('.mud-popover-open')")]
    [InlineData("await expect(row).toHaveClass(/overdue/)")]
    public void AClassLocatorFails(string line)
    {
        Write("helpers/rows.ts", line);

        var failure = Assert.Throws<XunitException>(() => EndToEndLocatorGuard.AssertLocatorsAreAccessible(_root));

        Assert.Contains("helpers/rows.ts:1 locates by a CSS class", failure.Message);
    }

    [Fact]
    public void AnAllowlistedLinePasses()
    {
        Write("helpers/clock.ts", "const clock = page.locator('.mud-picker-paper');");
        Write("locator-allowlist.txt", "helpers/clock.ts: page.locator('.mud-picker-paper') — the clock has no role");

        EndToEndLocatorGuard.AssertLocatorsAreAccessible(_root);
    }

    [Fact]
    public void AnAllowlistedLineWithASecondClassLocatorFails()
    {
        Write("helpers/clock.ts", "page.locator('.mud-picker-paper').or(page.locator('.pspad-clock'))");
        Write("locator-allowlist.txt", "helpers/clock.ts: page.locator('.mud-picker-paper') — the clock has no role");

        var failure = Assert.Throws<XunitException>(() => EndToEndLocatorGuard.AssertLocatorsAreAccessible(_root));

        Assert.Contains("helpers/clock.ts:1 locates by a CSS class", failure.Message);
    }

    [Fact]
    public void AStaleAllowlistEntryFails()
    {
        Write("locator-allowlist.txt", "helpers/gone.ts: .pspad-gone — removed long ago");

        var failure = Assert.Throws<XunitException>(() => EndToEndLocatorGuard.AssertLocatorsAreAccessible(_root));

        Assert.Contains("matches no class locator", failure.Message);
    }

    [Fact]
    public void AMissingFolderFails()
    {
        Directory.Delete(Path.Combine(_root, "helpers"), recursive: true);

        var failure = Assert.Throws<XunitException>(() => EndToEndLocatorGuard.AssertLocatorsAreAccessible(_root));

        Assert.Contains("no helpers folder", failure.Message);
    }

    [Fact]
    public void NoFilesToScanFails()
    {
        File.Delete(Path.Combine(_root, "scenarios", "01-clean.spec.ts"));

        var failure = Assert.Throws<XunitException>(() => EndToEndLocatorGuard.AssertLocatorsAreAccessible(_root));

        Assert.Contains("no .ts files", failure.Message);
    }

    void Write(string relativePath, string content) =>
        File.WriteAllText(Path.Combine(_root, relativePath), content + Environment.NewLine);
}
