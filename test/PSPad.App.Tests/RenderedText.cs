using Bunit;
using Microsoft.AspNetCore.Components;

namespace PSPad.App.Tests;

public static class RenderedText
{
    public static string Text(this IRenderedComponent<IComponent> rendered) =>
        string.Concat(rendered.Nodes.Select(node => node.TextContent));
}
