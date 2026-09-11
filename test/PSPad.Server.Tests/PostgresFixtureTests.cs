using Npgsql;
using PSPad.TestInfrastructure;
using Shouldly;

namespace PSPad.Server.Tests;

[IntegrationTest]
#pragma warning disable xUnit1041
[Collection(PostgresCollection.Name)]
public class PostgresFixtureTests(PostgresFixture fixture)
#pragma warning restore xUnit1041
{
    [Fact]
    public async Task Container_database_is_reachable()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("select 1", connection);

        (await command.ExecuteScalarAsync()).ShouldBe(1);
    }

    [Fact]
    public void Connection_string_does_not_point_at_the_dev_database()
    {
        fixture.ConnectionString.ShouldNotContain("Host=postgres;");
    }
}
