using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Budgets;

[UnitTest]
public class CategoryNameTests
{
    [Fact]
    public void ANameIsTrimmed() => Assert.Equal("Eating Out", CategoryName.Normalize("  Eating Out "));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameIsRejected(string name) =>
        Assert.Equal("A category needs a name.",
            Assert.Throws<DomainRejectedException>(() => CategoryName.Normalize(name)).Message);

    [Fact]
    public void ANameOver40CharactersIsRejected() =>
        Assert.Equal("A category name is at most 40 characters.",
            Assert.Throws<DomainRejectedException>(() => CategoryName.Normalize(new string('a', 41))).Message);

    [Fact]
    public void LookupIgnoresCase()
    {
        string[] names = ["Home", "Eating Out"];

        Assert.Equal(1, CategoryName.IndexIn(names, "eating out"));
        Assert.Equal(-1, CategoryName.IndexIn(names, "Hobby"));
    }
}
