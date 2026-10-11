using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Tailwind.Traders.Web.Standalone.Data;
using Tailwind.Traders.Web.Standalone.Data.Entities;

namespace Tailwind.Traders.Web.Standalone.Ai
{
    public class SemanticSearchHit
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public string TypeCode { get; set; }
        public string TypeName { get; set; }
        public string BrandName { get; set; }
        /// <summary>Cosine similarity (1 - cosine distance), 0..1 for normalised vectors.</summary>
        public double Score { get; set; }
    }

    public class SemanticSearchResult
    {
        public string Query { get; set; }
        public string Model { get; set; }
        public IReadOnlyList<SemanticSearchHit> Results { get; set; }
    }

    public interface ISemanticSearchService
    {
        Task<SemanticSearchResult> SearchAsync(string query, int take, CancellationToken ct = default);
        Task<int> EmbedCatalogAsync(CancellationToken ct = default);
    }

    public class SemanticSearchService : ISemanticSearchService
    {
        private readonly CatalogDbContext db;
        private readonly IEmbeddingProvider embeddings;
        private readonly ILogger<SemanticSearchService> logger;
        private readonly string productImagesUrl;
        private readonly double minScore;

        public SemanticSearchService(CatalogDbContext db, IEmbeddingProvider embeddings, ILogger<SemanticSearchService> logger, IOptions<Settings> settings, IOptions<AiOptions> aiOptions)
        {
            this.db = db;
            this.embeddings = embeddings;
            this.logger = logger;
            productImagesUrl = settings.Value.ProductImagesUrl;
            minScore = aiOptions.Value.MinScore;
        }

        public async Task<SemanticSearchResult> SearchAsync(string query, int take, CancellationToken ct = default)
        {
            using var a = AppTelemetry.Source.StartActivity("product.semantic_search");
            a?.SetTag("tailwindtraders.search.query_length", query.Length);
            a?.SetTag("gen_ai.request.model", embeddings.ModelName);
            var qv = new Vector(await embeddings.EmbedTracedAsync(query, ct));

            var rows = await db.ProductEmbeddings.AsNoTracking()
                .Where(e => e.Model == embeddings.ModelName)
                .OrderBy(e => e.Embedding.CosineDistance(qv))           // ORDER BY embedding <=> @q  (HNSW index)
                .Take(take)
                .Select(e => new { e.Product, e.Product.Type, e.Product.Brand, Distance = e.Embedding.CosineDistance(qv) })
                .ToListAsync(ct);

            var hits = rows.Select(r => new SemanticSearchHit
            {
                Id = r.Product.Id, Name = r.Product.Name, Price = r.Product.Price,
                ImageUrl = $"{productImagesUrl}/{r.Product.ImageName}",
                TypeCode = r.Type.Code, TypeName = r.Type.Name, BrandName = r.Brand.Name,
                Score = Math.Round(1 - r.Distance, 4),
            }).Where(h => h.Score >= minScore).ToList();

            a?.SetTag("tailwindtraders.search.results", hits.Count);
            AppTelemetry.SemanticSearches.Add(1, new KeyValuePair<string, object>("result", hits.Count > 0 ? "match" : "no_match"));
            return new SemanticSearchResult { Query = query, Model = embeddings.ModelName, Results = hits };
        }

        /// <summary>
        /// Text that represents a product for embedding: name (twice, it is what shoppers search for), category,
        /// then feature descriptions. Brand is left out: demo brands such as "ElctroDrill" would make every
        /// product of that brand match "drill".
        /// </summary>
        public static string ProductText(ProductEntity p) =>
            string.Join(". ", new[] { p.Name, p.Name, p.Type?.Name }
                .Concat(p.Features.OrderBy(f => f.Id).Select(f => f.Description))
                .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));

        /// <summary>Idempotent: embeds products whose vector is missing, from another model, or whose text changed.</summary>
        public async Task<int> EmbedCatalogAsync(CancellationToken ct = default)
        {
            using var a = AppTelemetry.Source.StartActivity("catalog.embed");
            var products = await db.Products.AsNoTracking().Include(p => p.Type).Include(p => p.Brand).Include(p => p.Features)
                .AsSplitQuery().OrderBy(p => p.Id).ToListAsync(ct);
            var existing = await db.ProductEmbeddings.AsNoTracking()
                .Select(e => new { e.ProductId, e.Model, e.ContentHash }).ToDictionaryAsync(e => e.ProductId, ct);

            var todo = products.Select(p => (p, text: ProductText(p)))
                .Select(x => (x.p, x.text, hash: Embeddings.ContentHash(x.text)))
                .Where(x => !existing.TryGetValue(x.p.Id, out var e) || e.Model != embeddings.ModelName || e.ContentHash != x.hash)
                .ToList();
            a?.SetTag("tailwindtraders.embed.pending", todo.Count);
            if (todo.Count == 0) return 0;

            // Bounded parallelism for remote providers (Bedrock TPS limits); the SDK retries throttling.
            var vectors = new float[todo.Count][];
            await Parallel.ForEachAsync(Enumerable.Range(0, todo.Count), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
                async (i, c) => vectors[i] = await embeddings.EmbedTracedAsync(todo[i].text, c));

            var now = DateTime.UtcNow;
            for (var i = 0; i < todo.Count; i++)
            {
                var (p, _, hash) = todo[i];
                var row = new ProductEmbeddingEntity { ProductId = p.Id, Model = embeddings.ModelName, ContentHash = hash, Embedding = new Vector(vectors[i]), UpdatedAt = now };
                if (existing.ContainsKey(p.Id)) db.ProductEmbeddings.Update(row); else db.ProductEmbeddings.Add(row);
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            logger.LogInformation("Embedded {Count} products with {Model}.", todo.Count, embeddings.ModelName);
            return todo.Count;
        }
    }
}
