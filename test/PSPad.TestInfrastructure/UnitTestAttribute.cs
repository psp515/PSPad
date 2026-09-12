using Xunit.v3;

namespace PSPad.TestInfrastructure;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UnitTestAttribute : Attribute, ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>("Category", "Unit")];
}
