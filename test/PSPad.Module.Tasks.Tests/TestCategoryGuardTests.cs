using System.Reflection;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests;

[UnitTest]
public class TestCategoryGuardTests
{
    [Fact]
    public void EveryTestClassInThisAssemblyDeclaresACategory()
    {
        TestCategoryGuard.AssertEveryTestClassIsCategorised(Assembly.GetExecutingAssembly());
    }
}
