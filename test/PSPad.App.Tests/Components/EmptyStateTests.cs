using Bunit;
using Microsoft.AspNetCore.Components.Web;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class EmptyStateTests : Bunit.TestContext
{
    [Fact]
    public void ItShowsItsMessage()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var empty = Render<EmptyState>(parameters => parameters.Add(p => p.Message, "No tasks yet."));

        Assert.Contains("No tasks yet.", empty.Markup);
    }

    [Fact]
    public void ClickingAnywhereOnTheCardRaisesOnCreate()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));
        var created = false;

        var empty = Render<EmptyState>(parameters => parameters
            .Add(p => p.Message, "No tasks yet.")
            .Add(p => p.CreateText, "Add task")
            .Add(p => p.OnCreate, () => created = true));
        var card = empty.Find(".pspad-empty-state");
        card.Click();

        Assert.True(created);
        Assert.Equal("button", card.GetAttribute("role"));
        Assert.Equal("Add task", card.GetAttribute("aria-label"));
        Assert.Empty(empty.FindAll("button"));
    }

    [Fact]
    public void EnterOnTheFocusedCardRaisesOnCreate()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));
        var created = false;

        var empty = Render<EmptyState>(parameters => parameters
            .Add(p => p.Message, "No tasks yet.")
            .Add(p => p.OnCreate, () => created = true));
        empty.Find(".pspad-empty-state").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.True(created);
    }

    [Fact]
    public void WithoutOnCreateTheCardIsNotAButton()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var empty = Render<EmptyState>(parameters => parameters.Add(p => p.Message, "Nothing here."));

        Assert.Null(empty.Find(".pspad-empty-state").GetAttribute("role"));
    }
}
