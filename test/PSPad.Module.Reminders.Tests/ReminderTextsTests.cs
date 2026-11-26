using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Reminders.Tests;

[UnitTest]
public class ReminderTextsTests
{
    private static TodoTask Task(string name)
    {
        var task = new TodoTask();
        task.Apply(new TaskCreated(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), name));
        return task;
    }

    [Theory]
    [InlineData("en", 0, "Due today")]
    [InlineData("en", 1, "Due tomorrow")]
    [InlineData("en", 5, "Due in 5 days")]
    [InlineData("pl", 0, "Termin dziś")]
    [InlineData("pl", 1, "Termin jutro")]
    [InlineData("pl", 5, "Termin za 5 dni")]
    [InlineData("de", 5, "Due in 5 days")]
    public void Body_follows_days_and_language(string language, int days, string expected) =>
        Assert.Equal(expected, ReminderTexts.For(Task("Buy milk"), days, language, "https://pspad.example/").Body);

    [Fact]
    public void Title_is_the_task_name_and_url_opens_the_task()
    {
        var task = Task("Buy milk");

        var note = ReminderTexts.For(task, 0, "en", "https://pspad.example/");

        Assert.Equal("Buy milk", note.Title);
        Assert.Equal($"https://pspad.example/?task={task.Id}", note.Url);
        Assert.Equal(task.Id.ToString(), note.Tag);
    }

    [Fact]
    public void Test_note_is_localized()
    {
        var note = ReminderTexts.Test("pl", "https://pspad.example/");

        Assert.Equal("Powiadomienia PSPad działają", note.Title);
        Assert.Equal("Tak będą wyglądać przypomnienia.", note.Body);
        Assert.Equal("https://pspad.example/", note.Url);
        Assert.Equal("pspad-test", note.Tag);
    }
}
