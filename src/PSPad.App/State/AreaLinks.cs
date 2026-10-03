namespace PSPad.App.State;

public static class AreaLinks
{
    public static string For(Guid areaId) => areaId == SharedWithMe.AreaId ? SharedWithMe.Href : $"/areas/{areaId}";
}
