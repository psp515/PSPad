using System.Reflection;

namespace PSPad.TestInfrastructure;

public static class AssemblyReferenceGuard
{
    public static void AssertReferencesNone(Assembly assembly, params string[] forbiddenPrefixes)
    {
        var violations = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => forbiddenPrefixes.Any(prefix =>
                name.StartsWith(prefix, StringComparison.Ordinal)))
            .OrderBy(name => name)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"{assembly.GetName().Name} must not reference: {string.Join(", ", violations)}");
    }
}
