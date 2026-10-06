using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Components;
using PSPad.App.State;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class InviteLinkCardTests : Bunit.TestContext
{
    const string Token = "invite-token-1234567890ab";
    const string Code = "K7M4PX";
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid Owner = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);
    static readonly DateTimeOffset Midnight = new(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    [Fact]
    public async Task CreatingALinkSharesTheList()
    {
        var list = NewList(User);
        var replica = AppTestHost.Arrange(this, User, Today, list);
        Services.GetRequiredService<AppState>().DisplayName = "Lukasz";

        var card = RenderCard(list);
        card.Find(".pspad-invite-create").Click();

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.Equal(24, stored!.InviteToken!.Length);
        Assert.Equal("Lukasz", stored.OwnerName);
    }

    [Fact]
    public void ALiveLinkShowsMinutesLeftAndAQrCode()
    {
        var list = Share(NewList(User), Midnight);
        AppTestHost.Arrange(this, User, Today, list);

        var card = RenderCard(list);

        Assert.Contains("30 min left", card.Find(".pspad-invite-left").TextContent);
        Assert.NotNull(card.Find("img.pspad-qr"));
    }

    [Fact]
    public void AnExpiredLinkOffersANewOneAndNoQrCode()
    {
        var list = Share(NewList(User), Midnight.AddHours(-1));
        AppTestHost.Arrange(this, User, Today, list);

        var card = RenderCard(list);

        Assert.Empty(card.FindAll("img.pspad-qr"));
        Assert.NotNull(card.Find(".pspad-invite-create"));
        Assert.Contains("Expired", card.Markup);
    }

    [Fact]
    public async Task EndingTheLinkStopsSharing()
    {
        var list = Share(NewList(User), Midnight);
        var replica = AppTestHost.Arrange(this, User, Today, list);

        var card = RenderCard(list);
        card.Find(".pspad-invite-end").Click();

        Assert.Null((await replica.LoadAsync<TaskList>(list.Id))!.InviteToken);
    }

    [Fact]
    public void ALiveInviteShowsTheCodeAndAPlainLink()
    {
        var list = Share(NewList(User), Midnight);
        AppTestHost.Arrange(this, User, Today, list);

        var card = RenderCard(list);

        Assert.Contains("K7M-4PX", card.Find(".pspad-invite-code").TextContent);
        Assert.DoesNotContain("#code=", card.Find(".pspad-share-link input").GetAttribute("value"));
    }

    [Fact]
    public void AnInviteClosedByWrongCodesSaysWhy()
    {
        var list = Share(NewList(User), Midnight);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            list.ApplyAll(TaskList.Decide(list, new JoinTaskList(Guid.NewGuid(), Owner, list.Id, Token, "AAAAAA", "X"), Midnight));
        }
        AppTestHost.Arrange(this, User, Today, list);

        var card = RenderCard(list);

        Assert.NotNull(card.Find(".pspad-invite-closed"));
        Assert.NotNull(card.Find(".pspad-invite-create"));
    }

    IRenderedComponent<InviteLinkCard> RenderCard(TaskList list) =>
        Render<InviteLinkCard>(parameters => parameters.Add(card => card.List, list));

    static TaskList NewList(Guid userId)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), userId, Guid.NewGuid(), Guid.NewGuid(), "Zakupy"), DateTimeOffset.UnixEpoch));
        return list;
    }

    static TaskList Share(TaskList list, DateTimeOffset at)
    {
        list.ApplyAll(TaskList.Decide(
            list, new ShareTaskList(Guid.NewGuid(), list.UserId, list.Id, Token, Code, "Lukasz"), at));
        return list;
    }
}
