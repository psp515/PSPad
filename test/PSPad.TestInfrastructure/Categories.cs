using Xunit.Abstractions;
using Xunit.Sdk;

namespace PSPad.TestInfrastructure;

public static class Categories
{
    public const string Key = "Category";
    public const string Unit = "Unit";
    public const string Integration = "Integration";
}

[TraitDiscoverer("PSPad.TestInfrastructure.CategoryDiscoverer", "PSPad.TestInfrastructure")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class UnitTestAttribute : Attribute, ITraitAttribute;

[TraitDiscoverer("PSPad.TestInfrastructure.CategoryDiscoverer", "PSPad.TestInfrastructure")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IntegrationTestAttribute : Attribute, ITraitAttribute;

public sealed class CategoryDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var attributeName = ((IReflectionAttributeInfo)traitAttribute).Attribute.GetType().Name;
        var category = attributeName.StartsWith("Integration", StringComparison.Ordinal)
            ? Categories.Integration
            : Categories.Unit;

        yield return new KeyValuePair<string, string>(Categories.Key, category);
    }
}
