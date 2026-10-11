using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;
using InboxAggregate = PSPad.Module.Tasks.Inbox.Inbox;

namespace PSPad.Module.Tasks.Tests;

[UnitTest]
public class ContentLimitsTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Member = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly DateTimeOffset Now = new(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);
    const string Token = "k3Jv9s2mQ0x7b1nR4tYw8eZa";
    const string Code = "K7M4PX";

    static readonly string LongestName = new('n', ContentLimits.Name);
    static readonly string TooLongName = new('n', ContentLimits.Name + 1);

    [Fact]
    public void TheLimitsAreTheAgreedOnes()
    {
        Assert.Equal(200, ContentLimits.Name);
        Assert.Equal(100, ContentLimits.Label);
        Assert.Equal(2_000, ContentLimits.FieldValue);
        Assert.Equal(20_000, ContentLimits.Description);
        Assert.Equal(100, ContentLimits.StepsPerTask);
        Assert.Equal(100, ContentLimits.FieldsPerItem);
        Assert.Equal(100, ContentLimits.MembersPerList);
    }

    [Fact]
    public void ANameAtTheLimitIsAcceptedEverywhere()
    {
        Assert.NotEmpty(Area.Decide(null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), LongestName, 0), Now));
        Assert.NotEmpty(Goal.Decide(null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), LongestName), Now));
        Assert.NotEmpty(TaskList.Decide(null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), LongestName), Now));
        Assert.NotEmpty(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), LongestName), Now));
        Assert.NotEmpty(ReferenceItem.Decide(null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), LongestName, 0), Now));
    }

    [Fact]
    public void ANameWhoseOnlyExcessIsWhitespaceIsAccepted()
    {
        var created = Assert.IsType<AreaCreated>(Assert.Single(
            Area.Decide(null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), $"  {LongestName}  ", 0), Now)));

        Assert.Equal(LongestName, created.Name);
    }

    [Fact]
    public void AnAreaNameOverTheLimitIsRejected()
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Home", 0), Now));

        Assert.Throws<DomainRejectedException>(() =>
            Area.Decide(null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), TooLongName, 0), Now));
        Assert.Throws<DomainRejectedException>(() =>
            Area.Decide(area, new RenameArea(Guid.NewGuid(), User, area.Id, TooLongName), Now));
    }

    [Fact]
    public void AGoalNameOverTheLimitIsRejected()
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), "Run"), Now));

        Assert.Throws<DomainRejectedException>(() =>
            Goal.Decide(null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), TooLongName), Now));
        Assert.Throws<DomainRejectedException>(() =>
            Goal.Decide(goal, new RenameGoal(Guid.NewGuid(), User, goal.Id, TooLongName), Now));
    }

    [Fact]
    public void AListNameOverTheLimitIsRejected()
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Errands"), Now));

        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), TooLongName), Now));
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(list, new RenameTaskList(Guid.NewGuid(), User, list.Id, TooLongName), Now));
    }

    [Fact]
    public void ATaskOrStepNameOverTheLimitIsRejected()
    {
        var task = Task();
        var stepId = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, stepId, "Level the bed"), Now));

        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), TooLongName), Now));
        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(task, new RenameTask(Guid.NewGuid(), User, task.Id, TooLongName), Now));
        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), TooLongName), Now));
        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(task, new RenameStep(Guid.NewGuid(), User, task.Id, stepId, TooLongName), Now));
    }

    [Fact]
    public void AnInboxItemOverTheNameLimitIsRejected()
    {
        var inbox = new InboxAggregate();
        var itemId = Guid.NewGuid();
        inbox.ApplyAll(InboxAggregate.Decide(null, new CreateInbox(Guid.NewGuid(), User, Guid.NewGuid()), Now));
        inbox.ApplyAll(InboxAggregate.Decide(inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, itemId, "Buy filament"), Now));

        Assert.Throws<DomainRejectedException>(() =>
            InboxAggregate.Decide(inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), TooLongName), Now));
        Assert.Throws<DomainRejectedException>(() =>
            InboxAggregate.Decide(inbox, new RenameInboxItem(Guid.NewGuid(), User, inbox.Id, itemId, TooLongName), Now));
    }

    [Fact]
    public void ATaskDescriptionOverTheLimitIsRejected()
    {
        var task = Task();

        Assert.NotEmpty(TodoTask.Decide(
            task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, new string('d', ContentLimits.Description)), Now));
        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, new string('d', ContentLimits.Description + 1)), Now));
    }

    [Fact]
    public void AStepPastTheLimitIsRejected()
    {
        var task = Task();
        for (var index = 0; index < ContentLimits.StepsPerTask; index++)
        {
            task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), $"Step {index}"), Now));
        }

        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), "One too many"), Now));
    }

    [Fact]
    public void AReferenceItemNameOrDescriptionOverTheLimitIsRejected()
    {
        var item = Item();

        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), TooLongName, 0), Now));
        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(item, new RenameReferenceItem(Guid.NewGuid(), User, item.Id, TooLongName), Now));
        Assert.NotEmpty(ReferenceItem.Decide(
            item, new SetReferenceItemDescription(Guid.NewGuid(), User, item.Id, new string('d', ContentLimits.Description)), Now));
        Assert.Throws<DomainRejectedException>(() => ReferenceItem.Decide(
            item, new SetReferenceItemDescription(Guid.NewGuid(), User, item.Id, new string('d', ContentLimits.Description + 1)), Now));
    }

    [Fact]
    public void AFieldLabelOrValueOverTheLimitIsRejected()
    {
        var item = Item();
        var fieldId = Guid.NewGuid();
        var longestLabel = new string('l', ContentLimits.Label);
        var longestValue = new string('v', ContentLimits.FieldValue);
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, fieldId, longestLabel, longestValue, null), Now));

        Assert.Throws<DomainRejectedException>(() => ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), longestLabel + "l", "v", null), Now));
        Assert.Throws<DomainRejectedException>(() => ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), "Label", longestValue + "v", null), Now));
        Assert.Throws<DomainRejectedException>(() => ReferenceItem.Decide(
            item, new EditReferenceField(Guid.NewGuid(), User, item.Id, fieldId, longestLabel + "l", "v", null), Now));
        Assert.Throws<DomainRejectedException>(() => ReferenceItem.Decide(
            item, new EditReferenceField(Guid.NewGuid(), User, item.Id, fieldId, "Label", longestValue + "v", null), Now));
    }

    [Fact]
    public void AFieldPastTheLimitIsRejected()
    {
        var item = Item();
        for (var index = 0; index < ContentLimits.FieldsPerItem; index++)
        {
            item.ApplyAll(ReferenceItem.Decide(
                item, new AddReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), $"Field {index}", "", null), Now));
        }

        Assert.Throws<DomainRejectedException>(() => ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), "One too many", "", null), Now));
    }

    [Fact]
    public void AJoinPastTheMemberLimitIsRejected()
    {
        var list = SharedList();
        for (var index = 0; index < ContentLimits.MembersPerList; index++)
        {
            list.Apply(new TaskListJoined(list.Id, User, Now, Guid.NewGuid(), $"Member {index}"));
        }

        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(list, new JoinTaskList(Guid.NewGuid(), Member, list.Id, Token, Code, "Anna"), Now));
    }

    [Fact]
    public void AnOverlongMemberNameIsCutToTheNameLimitInsteadOfRejected()
    {
        var list = SharedList();

        var joined = Assert.IsType<TaskListJoined>(Assert.Single(
            TaskList.Decide(list, new JoinTaskList(Guid.NewGuid(), Member, list.Id, Token, Code, TooLongName), Now)));

        Assert.Equal(LongestName, joined.DisplayName);
    }

    [Fact]
    public void AnOverlongOwnerNameIsCutToTheNameLimitWhenSharing()
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Errands"), Now));

        var shared = Assert.IsType<TaskListShared>(Assert.Single(
            TaskList.Decide(list, new ShareTaskList(Guid.NewGuid(), User, list.Id, Token, Code, TooLongName), Now)));

        Assert.Equal(LongestName, shared.OwnerName);
    }

    static TodoTask Task()
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Print a benchy"), Now));
        return task;
    }

    static ReferenceItem Item()
    {
        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "PLA Black", 0), Now));
        return item;
    }

    static TaskList SharedList()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(Guid.NewGuid(), User, Now, Guid.NewGuid(), "Errands"));
        list.Apply(new TaskListShared(list.Id, User, Now, Token, "Owner", Code));
        return list;
    }
}
