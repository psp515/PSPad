using System.Reflection;
using Xunit;

namespace PSPad.TestInfrastructure;

public static class CategoryGuard
{
    public static void AssertAllTestsCategorized(Assembly assembly)
    {
        var violations = assembly.GetTypes()
            .Where(HasTestMethod)
            .Where(type => CategoryCount(type) != 1)
            .Select(type => type.FullName)
            .ToArray();

        if (violations.Length > 0)
        {
            throw new InvalidOperationException(
                "Test classes must carry exactly one of [UnitTest] / [IntegrationTest]: "
                + string.Join(", ", violations));
        }
    }

    static bool HasTestMethod(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Any(method => method.GetCustomAttributes(inherit: true).Any(attribute => attribute is FactAttribute));

    static int CategoryCount(Type type) =>
        (type.GetCustomAttribute<UnitTestAttribute>() is null ? 0 : 1)
        + (type.GetCustomAttribute<IntegrationTestAttribute>() is null ? 0 : 1);
}
