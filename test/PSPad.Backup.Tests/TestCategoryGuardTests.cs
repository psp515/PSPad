using System.Reflection;
using PSPad.TestInfrastructure;

namespace PSPad.Backup.Tests;

[UnitTest]
public class TestCategoryGuardTests
{
    [Fact]
    public void EveryTestClassInThisAssemblyDeclaresACategory()
    {
        TestCategoryGuard.AssertEveryTestClassIsCategorised(Assembly.GetExecutingAssembly());
    }
}
