using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Xunit;

namespace Tailwind.Traders.Web.Tests
{
    /// <summary>
    /// Boots the website in "Standalone" mode (in-process APIs backed by PostgreSQL (EF Core) + MongoDB).
    /// If TT_PG_CONNECTION / TT_MONGO_CONNECTION are set, those are used; otherwise
    /// Testcontainers starts PostgreSQL 17 and MongoDB 7 in Docker.
    /// </summary>
    public class StandaloneFixture : IAsyncLifetime
    {
        private PostgreSqlContainer _pg;
        private MongoDbContainer _mongo;
        public WebApplicationFactory<Program> Factory { get; private set; }
        public string ConnectionString { get; private set; }

        public async Task InitializeAsync()
        {
            var pgCs = Environment.GetEnvironmentVariable("TT_PG_CONNECTION");
            var mongoCs = Environment.GetEnvironmentVariable("TT_MONGO_CONNECTION");

            if (string.IsNullOrEmpty(pgCs))
            {
                _pg = new PostgreSqlBuilder().WithImage("postgres:17").WithDatabase("tailwind").Build();
                await _pg.StartAsync();
                pgCs = _pg.GetConnectionString();
            }
            if (string.IsNullOrEmpty(mongoCs))
            {
                _mongo = new MongoDbBuilder().WithImage("mongo:7").Build();
                await _mongo.StartAsync();
                mongoCs = _mongo.GetConnectionString();
            }

            // Startup reads these while configuring services, so set them as process env vars.
            Environment.SetEnvironmentVariable("ConnectionStrings__Products", pgCs);
            Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "true"); // migrations + seed at startup
            ConnectionString = pgCs;
            Environment.SetEnvironmentVariable("MongoConnectionString", mongoCs);

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
            if (_mongo != null) await _mongo.DisposeAsync();
        }
    }

    [CollectionDefinition("standalone")]
    public class StandaloneCollection : ICollectionFixture<StandaloneFixture> { }
}
