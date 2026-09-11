using Testcontainers.PostgreSql;
using Xunit;

namespace PSPad.TestInfrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("pspad_test")
        .WithUsername("pspad")
        .WithPassword("pspad")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
