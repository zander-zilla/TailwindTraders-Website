using System;
using System.Collections.Generic;
using Pgvector;

namespace Tailwind.Traders.Web.Standalone.Data.Entities
{
    // EF Core entities for the product catalog (PostgreSQL). Ids come from the seed data (not database-generated).
    // API models in Standalone/Models are kept unchanged; ProductService maps entities -> models.

    public class BrandEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class TypeEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
    }

    public class TagEntity
    {
        public int Id { get; set; }
        public string Value { get; set; }
    }

    public class ProductEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string ImageName { get; set; }
        public int BrandId { get; set; }
        public BrandEntity Brand { get; set; }
        public int TypeId { get; set; }
        public TypeEntity Type { get; set; }
        public int? TagId { get; set; }
        public TagEntity Tag { get; set; }
        public List<FeatureEntity> Features { get; set; } = new();
    }

    /// <summary>Phase 8: one embedding per product and model (pgvector). Kept out of `products` so models can change.</summary>
    public class ProductEmbeddingEntity
    {
        public int ProductId { get; set; }
        public ProductEntity Product { get; set; }
        public string Model { get; set; }
        /// <summary>Hash of the embedded text; re-embed when the product text changes.</summary>
        public string ContentHash { get; set; }
        public Vector Embedding { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class FeatureEntity
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int ProductItemId { get; set; }
        public ProductEntity Product { get; set; }
    }
}
