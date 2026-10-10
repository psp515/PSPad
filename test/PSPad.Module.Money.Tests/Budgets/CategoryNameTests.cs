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

    [Fact]
    public void ANameOfExactly40CharactersIsAllowed() =>
        Assert.Equal(new string('a', 40), CategoryName.Normalize(new string('a', 40)));

    [Fact]
    public void LookupTrimsTheName()
    {
        string[] names = ["Home", "Food"];

        Assert.Equal(1, CategoryName.IndexIn(names, "  food "));
    }

    [Fact]
    public void ResolvingAKnownNameKeepsTheStoredSpelling()
    {
        string[] names = ["Home", "Eating Out"];

        Assert.Equal("Eating Out", CategoryName.Resolve(names, " eating OUT "));
    }

    [Fact]
    public void ResolvingANewNameNormalisesIt()
    {
        string[] names = ["Home"];

        Assert.Equal("Hobby", CategoryName.Resolve(names, "  Hobby "));
    }

    [Fact]
    public void ResolvingABlankNameIsRejected() =>
        Assert.Equal("A category needs a name.",
            Assert.Throws<DomainRejectedException>(() => CategoryName.Resolve(["Home"], "  ")).Message);
}
