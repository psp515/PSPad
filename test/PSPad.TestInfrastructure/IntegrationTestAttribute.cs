using Xunit.v3;

namespace PSPad.TestInfrastructure;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationTestAttribute : Attribute, ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>("Category", "Integration")];
}
