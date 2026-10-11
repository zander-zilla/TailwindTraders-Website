using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;
using Xunit;

namespace Tailwind.Traders.Web.Tests
{
    /// <summary>
    /// Boots the website in "Standalone" mode (in-process APIs backed by SQL Server + MongoDB).
    /// If TT_SQL_CONNECTION / TT_MONGO_CONNECTION are set, those are used; otherwise
    /// Testcontainers starts SQL Server 2022 and MongoDB 7 in Docker.
    /// </summary>
    public class StandaloneFixture : IAsyncLifetime
    {
        private MsSqlContainer _sql;
        private MongoDbContainer _mongo;
        public WebApplicationFactory<Startup> Factory { get; private set; }

        public async Task InitializeAsync()
        {
            var sqlCs = Environment.GetEnvironmentVariable("TT_SQL_CONNECTION");
            var mongoCs = Environment.GetEnvironmentVariable("TT_MONGO_CONNECTION");

            if (string.IsNullOrEmpty(sqlCs))
            {
                _sql = new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build();
                await _sql.StartAsync();
                sqlCs = _sql.GetConnectionString();
            }
            if (string.IsNullOrEmpty(mongoCs))
            {
                _mongo = new MongoDbBuilder().WithImage("mongo:7").Build();
                await _mongo.StartAsync();
                mongoCs = _mongo.GetConnectionString();
            }

            // Startup reads these while configuring services, so set them as process env vars.
            Environment.SetEnvironmentVariable("SqlConnectionString", sqlCs);
            Environment.SetEnvironmentVariable("MongoConnectionString", mongoCs);

            Factory = new WebApplicationFactory<Startup>().WithWebHostBuilder(b =>
            {
                // Not "Development": avoids launching the React dev server.
                b.UseEnvironment("Testing");
            });
            _ = Factory.CreateClient(); // force startup (DB seeding)
        }

        public async Task DisposeAsync()
        {
            Factory?.Dispose();
            if (_sql != null) await _sql.DisposeAsync();
            if (_mongo != null) await _mongo.DisposeAsync();
        }
    }

    [CollectionDefinition("standalone")]
    public class StandaloneCollection : ICollectionFixture<StandaloneFixture> { }
}
