using Bunit;
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
    public void ClickingCreateRaisesOnCreate()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));
        var created = false;

        var empty = Render<EmptyState>(parameters => parameters
            .Add(p => p.Message, "No tasks yet.")
            .Add(p => p.CreateText, "Add task")
            .Add(p => p.OnCreate, () => created = true));
        empty.Find(".pspad-empty-create").Click();

        Assert.True(created);
        Assert.Contains("Add task", empty.Find(".pspad-empty-create").TextContent);
    }

    [Fact]
    public void WithoutOnCreateThereIsNoCreateButton()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var empty = Render<EmptyState>(parameters => parameters.Add(p => p.Message, "Nothing here."));

        Assert.Empty(empty.FindAll(".pspad-empty-create"));
    }
}
