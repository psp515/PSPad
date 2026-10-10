using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class CategoryChoicesTests
{
    static readonly string[] Names = ["Home", "Food", "Eating Out"];

    [Fact]
    public void NoTextOffersEveryName() => Assert.Equal(Names, CategoryChoices.Search(Names, null));

    [Fact]
    public void TextMatchesAnywhereIgnoringCase() =>
        Assert.Equal(["Home", "Food", "Eating Out", "o"], CategoryChoices.Search(Names, "o"));

    [Fact]
    public void AnExactNameOffersNoCreateOption() => Assert.Equal(["Food"], CategoryChoices.Search(Names, " food "));

    [Fact]
    public void AnUnknownNameIsOfferedForCreation() => Assert.Equal(["Hobby"], CategoryChoices.Search(Names, " Hobby "));

    [Fact]
    public void OnlyUnknownNamesAreNew()
    {
        Assert.True(CategoryChoices.IsNew(Names, "Hobby"));
        Assert.False(CategoryChoices.IsNew(Names, "eating out"));
    }
}
