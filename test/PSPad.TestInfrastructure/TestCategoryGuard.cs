using System.Reflection;

namespace PSPad.TestInfrastructure;

public static class TestCategoryGuard
{
    public static void AssertEveryTestClassIsCategorised(Assembly assembly)
    {
        var unmarked = assembly.GetTypes()
            .Where(HasTestMethods)
            .Where(type => !IsCategorised(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name)
            .ToArray();

        Assert.True(
            unmarked.Length == 0,
            $"Test classes without a category attribute: {string.Join(", ", unmarked)}");
    }

    static bool HasTestMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Any(method => method.GetCustomAttributes()
                .Any(attribute => attribute is FactAttribute or TheoryAttribute));

    static bool IsCategorised(Type type) =>
        type.GetCustomAttribute<UnitTestAttribute>() is not null ||
        type.GetCustomAttribute<IntegrationTestAttribute>() is not null;
}
