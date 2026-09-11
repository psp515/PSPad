using PSPad.TestInfrastructure;

namespace PSPad.Server.Tests;

[UnitTest]
public class CategoryComplianceTests
{
    [Fact]
    public void All_tests_declare_a_category()
    {
        CategoryGuard.AssertAllTestsCategorized(typeof(PostgresFixtureTests).Assembly);
    }
}
