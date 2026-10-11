using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Tailwind.Traders.Web.Standalone.Data.Entities;

namespace Tailwind.Traders.Web.Standalone.Data
{
    public class CatalogDbContext : DbContext
    {
        public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

        public DbSet<BrandEntity> Brands => Set<BrandEntity>();
        public DbSet<TypeEntity> Types => Set<TypeEntity>();
        public DbSet<TagEntity> Tags => Set<TagEntity>();
        public DbSet<ProductEntity> Products => Set<ProductEntity>();
        public DbSet<FeatureEntity> Features => Set<FeatureEntity>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // snake_case table/column names come from UseSnakeCaseNamingConvention() (EFCore.NamingConventions).
            b.Entity<BrandEntity>(e =>
            {
                e.ToTable("brands");
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            });

            b.Entity<TypeEntity>(e =>
            {
                e.ToTable("types");
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Code).HasMaxLength(255).IsRequired();
                e.Property(x => x.Name).HasMaxLength(255).IsRequired();
                e.HasIndex(x => x.Code).IsUnique();               // filter key
            });

            b.Entity<TagEntity>(e =>
            {
                e.ToTable("tags");
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Value).HasMaxLength(255).IsRequired();
            });

            b.Entity<ProductEntity>(e =>
            {
                e.ToTable("products");
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Name).HasMaxLength(255).IsRequired();
                e.Property(x => x.Price).HasPrecision(9, 2);       // numeric(9,2) == legacy DECIMAL(9,2)
                e.Property(x => x.ImageName).HasMaxLength(255);
                // Real foreign keys (legacy schema had none). Indexes on FK columns for the list/detail joins.
                e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.SetNull);
                e.HasMany(x => x.Features).WithOne(f => f.Product).HasForeignKey(f => f.ProductItemId).OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<FeatureEntity>(e =>
            {
                e.ToTable("features");
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Title).HasMaxLength(255);
                e.Property(x => x.Description);                   // text (legacy NVARCHAR(MAX))
            });
        }

        public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
    }

    /// <summary>Used only by `dotnet ef` at design time (migrations); never connects.</summary>
    public class CatalogDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
    {
        public CatalogDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<CatalogDbContext>();
            CatalogDbContext.Configure(builder, "Host=localhost;Database=tailwind;Username=design;Password=design");
            return new CatalogDbContext(builder.Options);
        }
    }
}
