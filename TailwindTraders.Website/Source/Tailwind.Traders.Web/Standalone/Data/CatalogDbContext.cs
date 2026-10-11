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
        public DbSet<ProductEmbeddingEntity> ProductEmbeddings => Set<ProductEmbeddingEntity>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.HasPostgresExtension("vector"); // pgvector (PostgreSQL 17 image pgvector/pgvector:pg17; Aurora PostgreSQL supports it natively)

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

            b.Entity<ProductEmbeddingEntity>(e =>
            {
                e.ToTable("product_embeddings");
                e.HasKey(x => x.ProductId);
                e.HasOne(x => x.Product).WithOne().HasForeignKey<ProductEmbeddingEntity>(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
                e.Property(x => x.Model).HasMaxLength(128).IsRequired();
                e.Property(x => x.ContentHash).HasMaxLength(32).IsRequired();
                e.Property(x => x.Embedding).HasColumnType($"vector({Tailwind.Traders.Web.Standalone.Ai.Embeddings.Dimensions})").IsRequired();
                // Approximate nearest neighbour index for cosine distance (<=>).
                e.HasIndex(x => x.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
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
            options.UseNpgsql(connectionString, o => o.UseVector()).UseSnakeCaseNamingConvention();
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
