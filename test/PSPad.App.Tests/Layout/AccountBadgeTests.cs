using Bunit;
using PSPad.App.Layout;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class AccountBadgeTests : Bunit.TestContext
{
    static readonly Guid User = Guid.Parse("6f1d2c3b-0000-4000-8000-000000000001");

    [Fact]
    public void ItShowsTheNameAndEmail()
    {
        Arrange();

        var badge = Render<AccountBadge>(parameters => parameters
            .Add(account => account.DisplayName, "Ada Lovelace")
            .Add(account => account.Email, "ada@example.com")
            .Add(account => account.UserId, User));

        Assert.Contains("Ada Lovelace", badge.Markup);
        Assert.Contains("ada@example.com", badge.Markup);
    }

    [Fact]
    public void TheAvatarFallsBackToTheEmailWhenThereIsNoName()
    {
        Arrange();

        var badge = Render<AccountBadge>(parameters => parameters
            .Add(account => account.DisplayName, "")
            .Add(account => account.Email, "ada@example.com")
            .Add(account => account.UserId, User));

        Assert.Contains(">A<", badge.Markup);
    }

    [Fact]
    public void TheAvatarOnlyWrapperHasPadding()
    {
        Arrange();

        var badge = Render<AccountBadge>(parameters => parameters
            .Add(account => account.DisplayName, "Ada Lovelace")
            .Add(account => account.Email, "ada@example.com")
            .Add(account => account.UserId, User)
            .Add(account => account.AvatarOnly, true));

        var wrapper = badge.Find(".pspad-account-avatar-only");
        Assert.Contains("px-3", wrapper.ClassList);
        Assert.Contains("py-1", wrapper.ClassList);
    }

    [Fact]
    public void ItHasNoDropdownArrowOrMenu()
    {
        Arrange();

        var badge = Render<AccountBadge>(parameters => parameters
            .Add(account => account.DisplayName, "Ada Lovelace")
            .Add(account => account.Email, "ada@example.com")
            .Add(account => account.UserId, User));

        Assert.DoesNotContain("expand_more", badge.Markup);
        Assert.Empty(badge.FindAll(".mud-menu"));
    }

    void Arrange() => AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
}
