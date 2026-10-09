namespace PSPad.App.State;

public static class ActiveTab
{
    public static NavTab For(string baseRelativePath)
    {
        var path = baseRelativePath.Split('?', '#')[0].Trim('/');
        var firstSegment = path.Split('/')[0];

        return firstSegment switch
        {
            "" => NavTab.MyDay,
            "inbox" => NavTab.Inbox,
            "areas" or "lists" => NavTab.Areas,
            "goals" => NavTab.Goals,
            "budgets" => NavTab.Budgets,
            _ => NavTab.None
        };
    }
}
