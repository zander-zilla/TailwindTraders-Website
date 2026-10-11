using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tailwind.Traders.Web.Standalone.Data.Entities;

namespace Tailwind.Traders.Web.Standalone.Data
{
    /// <summary>
    /// Applies EF Core migrations and seeds the catalog from CSVs bundled in Standalone/Data/Seed
    /// (originally from microsoft/TailwindTraders-Backend; no runtime download).
    /// Seeding is typed, runs in ONE transaction and is idempotent: only rows whose Id is missing are inserted,
    /// so re-running repairs a partial seed and never duplicates.
    /// Run it as a separate step: `dotnet Tailwind.Traders.Web.dll --migrate` (K8s Job / compose `migrate` service),
    /// or set Database:MigrateOnStartup=true for local dev/tests.
    /// </summary>
    public class StandaloneDatabaseInitializer
    {
        private readonly CatalogDbContext db;
        private readonly ILogger logger;
        private readonly string seedDirectory;

        public StandaloneDatabaseInitializer(CatalogDbContext db, ILogger logger, string seedDirectory = null)
        {
            this.db = db;
            this.logger = logger;
            this.seedDirectory = seedDirectory ?? Path.Combine(AppContext.BaseDirectory, "Standalone", "Data", "Seed");
        }

        public async Task MigrateAndSeedAsync(CancellationToken ct = default)
        {
            logger.LogInformation("Applying EF Core migrations...");
            await db.Database.MigrateAsync(ct);

            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var added = 0;
                added += await AddMissing(db.Brands, Read<BrandEntity, BrandMap>("ProductBrands.csv"), ct);
                added += await AddMissing(db.Types, Read<TypeEntity, TypeMap>("ProductTypes.csv"), ct);
                added += await AddMissing(db.Tags, Read<TagEntity, TagMap>("ProductTags.csv"), ct);
                added += await AddMissing(db.Products, Read<ProductEntity, ProductMap>("ProductItems.csv"), ct);
                added += await AddMissing(db.Features, Read<FeatureEntity, FeatureMap>("ProductFeatures.csv"), ct);
                await tx.CommitAsync(ct);
                logger.LogInformation(added == 0 ? "Seeding not required." : "Seeding completed: {Count} rows inserted.", added);
            });
        }

        private async Task<int> AddMissing<T>(DbSet<T> set, List<T> rows, CancellationToken ct) where T : class
        {
            var idProp = typeof(T).GetProperty("Id");
            var existing = (await set.AsNoTracking().ToListAsync(ct)).Select(e => (int)idProp.GetValue(e)).ToHashSet();
            var missing = rows.Where(r => !existing.Contains((int)idProp.GetValue(r))).ToList();
            if (missing.Count == 0) return 0;
            set.AddRange(missing);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            return missing.Count;
        }

        private List<T> Read<T, TMap>(string file) where TMap : ClassMap<T>
        {
            using var reader = new StreamReader(Path.Combine(seedDirectory, file));
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
            csv.Context.RegisterClassMap<TMap>();
            return csv.GetRecords<T>().ToList();
        }

        private sealed class BrandMap : ClassMap<BrandEntity>
        { public BrandMap() { Map(x => x.Id); Map(x => x.Name); } }

        private sealed class TypeMap : ClassMap<TypeEntity>
        { public TypeMap() { Map(x => x.Id); Map(x => x.Code); Map(x => x.Name); } }

        private sealed class TagMap : ClassMap<TagEntity>
        { public TagMap() { Map(x => x.Id); Map(x => x.Value); } }

        private sealed class ProductMap : ClassMap<ProductEntity>
        {
            public ProductMap()
            {
                Map(x => x.Id); Map(x => x.Name); Map(x => x.Price); Map(x => x.ImageName);
                Map(x => x.BrandId); Map(x => x.TypeId);
                Map(x => x.TagId).TypeConverterOption.NullValues(string.Empty); // blank TagId -> NULL
            }
        }

        private sealed class FeatureMap : ClassMap<FeatureEntity>
        {
            public FeatureMap() { Map(x => x.Id); Map(x => x.Title); Map(x => x.Description); Map(x => x.ProductItemId); }
        }
    }
}
