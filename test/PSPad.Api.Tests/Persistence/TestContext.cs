using Microsoft.Extensions.Options;
using PSPad.Infrastructure.Mongo;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

public static class TestContext
{
    public static MongoContext For(MongoFixture fixture)
    {
        MongoConventions.Register();
        return new MongoContext(Options.Create(new MongoOptions
        {
            ConnectionString = fixture.ConnectionString,
            Database = "pspad_test"
        }));
    }
}
