using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace PSPad.App.Tests.Localization;

public static class TestLocalizers
{
    public static IStringLocalizer<T> For<T>() =>
        new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<T>>();
}
