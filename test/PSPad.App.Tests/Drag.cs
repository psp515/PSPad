using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace PSPad.App.Tests;

public static class Drag
{
    public static void Drop<TItem>(IRenderedComponent<IComponent> host, TItem item, int toIndex)
        where TItem : notnull
    {
        var container = host.FindComponent<MudDropContainer<TItem>>();
        container.InvokeAsync(() => container.Instance.ItemDropped.InvokeAsync(
            new MudItemDropInfo<TItem>(item, "zone", toIndex))).GetAwaiter().GetResult();
    }

    public static void Drop<TItem>(IRenderedComponent<IComponent> host, Func<TItem, bool> which, int toIndex)
        where TItem : notnull =>
        Drop(host, host.FindComponent<MudDropContainer<TItem>>().Instance.Items!.Single(which), toIndex);
}
