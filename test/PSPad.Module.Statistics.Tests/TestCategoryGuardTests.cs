using System.Reflection;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class TestCategoryGuardTests
{
    [Fact]
    public void EveryTestClassInThisAssemblyDeclaresACategory()
    {
        TestCategoryGuard.AssertEveryTestClassIsCategorised(Assembly.GetExecutingAssembly());
    }
}
