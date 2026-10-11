using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.DynamoDb;
using Testcontainers.PostgreSql;
using Xunit;

namespace Tailwind.Traders.Web.Tests
{
    /// <summary>
    /// Boots the website in "Standalone" mode (in-process APIs backed by PostgreSQL (EF Core) + DynamoDB Local (cart)).
    /// If TT_PG_CONNECTION / TT_DYNAMODB_URL are set, those are used; otherwise
    /// Testcontainers starts PostgreSQL 17 and amazon/dynamodb-local in Docker.
    /// </summary>
    public class StandaloneFixture : IAsyncLifetime
    {
        private PostgreSqlContainer _pg;
        private DynamoDbContainer _ddb;
        public WebApplicationFactory<Program> Factory { get; private set; }
        public string ConnectionString { get; private set; }

        public async Task InitializeAsync()
        {
            var pgCs = Environment.GetEnvironmentVariable("TT_PG_CONNECTION");
            var ddbUrl = Environment.GetEnvironmentVariable("TT_DYNAMODB_URL");

            if (string.IsNullOrEmpty(pgCs))
            {
                _pg = new PostgreSqlBuilder().WithImage("postgres:17").WithDatabase("tailwind").Build();
                await _pg.StartAsync();
                pgCs = _pg.GetConnectionString();
            }
            if (string.IsNullOrEmpty(ddbUrl))
            {
                _ddb = new DynamoDbBuilder().WithImage("amazon/dynamodb-local:latest").Build();
                await _ddb.StartAsync();
                ddbUrl = _ddb.GetConnectionString();
            }

            // Startup reads these while configuring services, so set them as process env vars.
            Environment.SetEnvironmentVariable("ConnectionStrings__Products", pgCs);
            Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "true"); // migrations + seed at startup
            ConnectionString = pgCs;
            Environment.SetEnvironmentVariable("Cart__ServiceUrl", ddbUrl);       // table created by the migrate step
            Environment.SetEnvironmentVariable("Cart__TableName", "tt-cart-tests");

            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                // Not "Development": avoids launching the React dev server.
                b.UseEnvironment("Testing");
            });
            _ = Factory.CreateClient(); // force startup (migrations + seeding)
        }

        public async Task DisposeAsync()
        {
            Factory?.Dispose();
            if (_pg != null) await _pg.DisposeAsync();
            if (_ddb != null) await _ddb.DisposeAsync();
        }
    }

    [CollectionDefinition("standalone")]
    public class StandaloneCollection : ICollectionFixture<StandaloneFixture> { }
}
