using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tailwind.Traders.Web.Standalone.Ai;
using Tailwind.Traders.Web.Standalone.Data;
using Xunit;

namespace Tailwind.Traders.Web.Tests
{
    /// <summary>Phase 8: pgvector semantic search with the offline (local hashing) embedding provider.</summary>
    [Collection("standalone")]
    public class SemanticSearchTests
    {
        private readonly StandaloneFixture _fx;
        public SemanticSearchTests(StandaloneFixture fx) => _fx = fx;

        private async Task<JsonElement> Search(string q)
        {
            var r = await _fx.Factory.CreateClient().GetAsync("/api/v1/products/search?q=" + System.Uri.EscapeDataString(q));
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        }

        private static string[] Names(JsonElement e) => e.GetProperty("results").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToArray();

        [Fact]
        public void Migrate_step_embedded_every_product_and_is_idempotent()
        {
            using var scope = _fx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            Assert.Equal(db.Products.Count(), db.ProductEmbeddings.Count(e => e.Model == "local-hash-v1"));
            var again = scope.ServiceProvider.GetRequiredService<ISemanticSearchService>().EmbedCatalogAsync().GetAwaiter().GetResult();
            Assert.Equal(0, again);
            // pgvector extension + HNSW index exist
            var conn = db.Database.GetDbConnection(); conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select count(*) from pg_indexes where indexname = 'ix_product_embeddings_embedding' and indexdef ilike '%hnsw%vector_cosine_ops%'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());
            conn.Close();
        }

        [Theory]
        [InlineData("drill", "Multi Function Drill")]
        [InlineData("watering can for my garden", "Metal Watering Can")]
        [InlineData("screwdrivers", "Screwdriver")]
        [InlineData("refridgerator", "Refrigerator")] // typo: character trigrams still match
        public async Task Search_ranks_the_expected_product_near_the_top(string q, string expectedPrefix)
        {
            var json = await Search(q);
            Assert.Equal("local-hash-v1", json.GetProperty("model").GetString());
            var names = Names(json);
            System.Console.WriteLine($"[search] {q}: " + string.Join(" | ", json.GetProperty("results").EnumerateArray().Take(4).Select(x => $"{x.GetProperty("name").GetString()}={x.GetProperty("score").GetDouble()}")));
            Assert.Contains(names.Take(3), n => n.StartsWith(expectedPrefix, System.StringComparison.OrdinalIgnoreCase) || n.Contains(expectedPrefix, System.StringComparison.OrdinalIgnoreCase));
            var scores = json.GetProperty("results").EnumerateArray().Select(x => x.GetProperty("score").GetDouble()).ToArray();
            Assert.Equal(scores.OrderByDescending(s => s), scores); // ordered by similarity
        }

        [Fact]
        public async Task Nonsense_query_returns_no_results_and_empty_query_is_400()
        {
            Assert.Empty(Names(await Search("zxqv jjkw")));
            var r = await _fx.Factory.CreateClient().GetAsync("/api/v1/products/search?q=");
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        [Fact]
        public async Task Search_emits_semantic_search_and_embedding_spans()
        {
            var spans = new ConcurrentBag<Activity>();
            using var l = new ActivityListener
            {
                ShouldListenTo = s => s.Name == AppTelemetry.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = a => spans.Add(a),
            };
            ActivitySource.AddActivityListener(l);
            await Search("garden gnome");
            var search = Assert.Single(spans, s => s.OperationName == "product.semantic_search" && s.GetTagItem("tailwindtraders.search.results") is int);
            var embed = Assert.Single(spans, s => s.OperationName == "embeddings local-hash-v1" && s.ParentSpanId == search.SpanId);
            Assert.Equal("embeddings", embed.GetTagItem("gen_ai.operation.name"));
            Assert.Equal("local", embed.GetTagItem("gen_ai.system"));
            Assert.Equal(Embeddings.Dimensions, embed.GetTagItem("gen_ai.embeddings.dimension.count"));
        }

        [Fact]
        public async Task Assistant_is_off_by_default_and_answers_offline_when_enabled()
        {
            var body = new StringContent("{\"question\":\"I need something to water plants\"}", Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.NotFound, (await _fx.Factory.CreateClient().PostAsync("/api/v1/assistant", body)).StatusCode);

            using var enabled = _fx.Factory.WithWebHostBuilder(b => b.UseSetting("Ai:Assistant:Enabled", "true"));
            var r = await enabled.CreateClient().PostAsync("/api/v1/assistant",
                new StringContent("{\"question\":\"I need something to water plants\"}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("offline-template", json.GetProperty("model").GetString());
            Assert.Contains(json.GetProperty("products").EnumerateArray(), p => p.GetProperty("name").GetString().Contains("Watering") || p.GetProperty("typeCode").GetString() == "gardening");
        }

        [Fact]
        public void Local_embeddings_are_deterministic_and_normalised()
        {
            var a = LocalHashingEmbeddingProvider.Embed("Cordless drill");
            var b = LocalHashingEmbeddingProvider.Embed("cordless DRILL!");
            Assert.Equal(a, b);
            Assert.Equal(1.0, System.Math.Sqrt(a.Sum(x => (double)x * x)), 3);
        }
    }
}
