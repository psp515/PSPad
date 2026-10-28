using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace PSPad.App.Tests;

public static class RenderedLabel
{
    public static IElement Labelled(this IRenderedComponent<IComponent> rendered, string label)
    {
        var target = rendered.FindAll("label").First(item => item.TextContent.Trim() == label).GetAttribute("for")!;
        return rendered.Find($"[id='{target}']");
    }
}
