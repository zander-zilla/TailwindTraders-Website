using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Options;

namespace Tailwind.Traders.Web.Standalone.Ai
{
    public interface IEmbeddingProvider
    {
        /// <summary>Stored with each vector; vectors of different models are never compared.</summary>
        string ModelName { get; }
        /// <summary>"local" or "aws.bedrock" (OpenTelemetry gen_ai.system).</summary>
        string System { get; }
        Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    }

    public static class Embeddings
    {
        /// <summary>Column size (vector(1024)). Titan Text Embeddings v2 supports 256/512/1024; local hashing uses 1024.</summary>
        public const int Dimensions = 1024;

        /// <summary>Wraps a provider call in a GenAI semantic-conventions span + metric.</summary>
        public static async Task<float[]> EmbedTracedAsync(this IEmbeddingProvider p, string text, CancellationToken ct = default)
        {
            using var a = AppTelemetry.Source.StartActivity($"embeddings {p.ModelName}", ActivityKind.Client);
            a?.SetTag("gen_ai.operation.name", "embeddings");
            a?.SetTag("gen_ai.system", p.System);
            a?.SetTag("gen_ai.request.model", p.ModelName);
            a?.SetTag("tailwindtraders.embedding.input_chars", text?.Length ?? 0);
            var sw = Stopwatch.StartNew();
            try
            {
                var v = await p.EmbedAsync(text ?? "", ct);
                a?.SetTag("gen_ai.embeddings.dimension.count", v.Length);
                return v;
            }
            catch (Exception e)
            {
                a?.SetStatus(ActivityStatusCode.Error, e.Message);
                throw;
            }
            finally
            {
                AppTelemetry.EmbeddingDuration.Record(sw.Elapsed.TotalSeconds,
                    new KeyValuePair<string, object>("gen_ai.request.model", p.ModelName));
            }
        }

        public static string ContentHash(string text) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).Substring(0, 16);

        internal static float[] Normalize(float[] v)
        {
            var norm = Math.Sqrt(v.Sum(x => (double)x * x));
            if (norm > 0) for (var i = 0; i < v.Length; i++) v[i] = (float)(v[i] / norm);
            return v;
        }
    }

    /// <summary>
    /// Offline, deterministic embedding (no model download, no network): signed feature hashing of
    /// word unigrams (light stemming), bigrams and character trigrams into 1024 dims, L2-normalised.
    /// Lexical + fuzzy ("drils" ~ "drill"), NOT truly semantic: good enough for local/kind/tests,
    /// use Bedrock for real semantics.
    /// </summary>
    public class LocalHashingEmbeddingProvider : IEmbeddingProvider
    {
        public string ModelName => "local-hash-v1";
        public string System => "local";
        private static readonly Regex Word = new("[a-z0-9]+", RegexOptions.Compiled);
        private static readonly HashSet<string> Stop = new(new[] { "a", "an", "and", "the", "for", "of", "with", "to", "in", "on", "is", "it", "i", "me", "my", "need", "want", "some", "something", "looking", "x" });

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => Task.FromResult(Embed(text));

        public static float[] Embed(string text)
        {
            var v = new float[Embeddings.Dimensions];
            var words = Word.Matches((text ?? "").ToLowerInvariant()).Select(m => Stem(m.Value)).Where(w => !Stop.Contains(w)).ToList();
            for (var i = 0; i < words.Count; i++)
            {
                Add(v, "w:" + words[i], 1.0f);
                if (i > 0) Add(v, "b:" + words[i - 1] + " " + words[i], 0.5f);
                var padded = "^" + words[i] + "$";
                for (var j = 0; j + 3 <= padded.Length; j++) Add(v, "c:" + padded.Substring(j, 3), 0.35f);
            }
            return Embeddings.Normalize(v);
        }

        private static string Stem(string w) =>
            w.Length > 4 && w.EndsWith("ies") ? w[..^3] + "y" :
            w.Length > 3 && w.EndsWith("es") && !w.EndsWith("ses") ? w[..^2] :
            w.Length > 3 && w.EndsWith("s") && !w.EndsWith("ss") ? w[..^1] : w;

        private static void Add(float[] v, string feature, float weight)
        {
            // FNV-1a 64-bit: stable across processes/platforms (string.GetHashCode is randomized).
            ulong h = 14695981039346656037UL;
            foreach (var ch in feature) { h ^= ch; h *= 1099511628211UL; }
            var idx = (int)(h % (ulong)v.Length);
            v[idx] += ((h >> 63) == 0 ? 1 : -1) * weight;
        }
    }

    /// <summary>Amazon Bedrock Titan Text Embeddings v2 (1024 dims, normalised). Credentials: AWS SDK default chain (EKS Pod Identity).</summary>
    public class BedrockEmbeddingProvider : IEmbeddingProvider
    {
        private readonly IAmazonBedrockRuntime bedrock;
        private readonly string modelId;
        public BedrockEmbeddingProvider(IAmazonBedrockRuntime bedrock, IOptions<AiOptions> options)
        {
            this.bedrock = bedrock;
            modelId = options.Value.EmbeddingModelId;
        }
        public string ModelName => modelId;
        public string System => "aws.bedrock";

        public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            var body = JsonSerializer.Serialize(new { inputText = text, dimensions = Embeddings.Dimensions, normalize = true });
            var resp = await bedrock.InvokeModelAsync(new InvokeModelRequest
            {
                ModelId = modelId,
                ContentType = "application/json",
                Accept = "application/json",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(body)),
            }, ct);
            using var doc = await JsonDocument.ParseAsync(resp.Body, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("inputTextTokenCount", out var tokens))
                Activity.Current?.SetTag("gen_ai.usage.input_tokens", tokens.GetInt32());
            return doc.RootElement.GetProperty("embedding").EnumerateArray().Select(e => e.GetSingle()).ToArray();
        }
    }
}
