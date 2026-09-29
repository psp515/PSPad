using Microsoft.AspNetCore.Components;

namespace PSPad.App.Markdown;

[EventHandler("onmarkdownviewclick", typeof(MarkdownViewClickEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
public static class EventHandlers
{
}
