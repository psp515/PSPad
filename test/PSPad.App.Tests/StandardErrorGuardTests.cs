using System.Reflection;
using PSPad.App.Layout;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests;

[UnitTest]
public class StandardErrorGuardTests
{
    const byte Call = 0x28;
    const byte CallVirt = 0x6F;
    const BindingFlags Everything =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // Blazor WebAssembly raises its "An unhandled error has occurred" banner on any stderr write,
    // so a handled failure logged there reads to the user as a crash.
    [Fact]
    public void TheAppNeverWritesToStandardError()
    {
        var assembly = typeof(AppShell).Assembly;

        var offenders = assembly.GetTypes()
            .SelectMany(type => type.GetMethods(Everything).Cast<MethodBase>().Concat(type.GetConstructors(Everything)))
            .Where(method => Calls(method).Any(IsStandardError))
            .Select(method => $"{method.DeclaringType?.FullName}.{method.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    static IEnumerable<MethodBase> Calls(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? [];

        for (var index = 0; index + 4 < il.Length; index++)
        {
            if (il[index] is not (Call or CallVirt))
            {
                continue;
            }

            var callee = Resolve(method.Module, BitConverter.ToInt32(il, index + 1));

            if (callee is not null)
            {
                yield return callee;
            }
        }
    }

    static MethodBase? Resolve(System.Reflection.Module module, int token)
    {
        try
        {
            return module.ResolveMethod(token);
        }
        catch (Exception exception) when (exception is ArgumentException or BadImageFormatException)
        {
            return null;
        }
    }

    static bool IsStandardError(MethodBase callee) =>
        callee.DeclaringType == typeof(Console) && callee.Name is "get_Error";
}
