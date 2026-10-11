using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tailwind.Traders.Web.Standalone;
using Tailwind.Traders.Web.Standalone.Data;
using Xunit;

namespace Tailwind.Traders.Web.Tests
{
    /// <summary>Phase 3a: PostgreSQL / EF Core specific behavior (search case-insensitivity, idempotent seed, schema).</summary>
    [Collection("standalone")]
    public class CatalogDataTests
    {
        private readonly StandaloneFixture _fx;
        public CatalogDataTests(StandaloneFixture fx) => _fx = fx;

        [Theory]
        [InlineData("microwave")]   // ONNX labels are lower case; product names are capitalized
        [InlineData("MICROWAVE")]
        [InlineData("Microwave")]
        public async Task Name_search_is_case_insensitive(string term)
        {
            using var scope = _fx.Factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IProductService>();
            var products = (await svc.GetProducts(searchTerm: term)).ToList();
            Assert.NotEmpty(products);
            Assert.All(products, p => Assert.Contains("microwave", p.Name.ToLowerInvariant()));
        }

        [Fact]
        public async Task Like_wildcards_in_search_term_are_literal()
        {
            using var scope = _fx.Factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IProductService>();
            Assert.Empty(await svc.GetProducts(searchTerm: "%"));
            Assert.Empty(await svc.GetProducts(searchTerm: "_"));
        }

        [Fact]
        public async Task Type_filter_is_case_insensitive_like_sql_server()
        {
            using var scope = _fx.Factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IProductService>();
            Assert.Equal(Baseline.FilterTypeProductCount, (await svc.GetProducts(typeCodes: new[] { "HOME" })).Count());
        }

        [Fact]
        public async Task Seeding_is_idempotent_and_counts_match_golden_values()
        {
            using var scope = _fx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await new StandaloneDatabaseInitializer(db, NullLogger.Instance).MigrateAndSeedAsync(); // second run

            Assert.Equal(Baseline.BrandCount, await db.Brands.CountAsync());
            Assert.Equal(Baseline.TypeCount, await db.Types.CountAsync());
            Assert.Equal(3, await db.Tags.CountAsync());
            Assert.Equal(Baseline.SeededProductCount, await db.Products.CountAsync());
            Assert.Equal(77, await db.Features.CountAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }

        [Fact]
        public async Task Seeding_repairs_a_partially_seeded_database()
        {
            using var scope = _fx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Features.Where(f => f.Id > 70).ExecuteDeleteAsync();
            await new StandaloneDatabaseInitializer(db, NullLogger.Instance).MigrateAndSeedAsync();
            Assert.Equal(77, await db.Features.CountAsync());
        }
    }
}
