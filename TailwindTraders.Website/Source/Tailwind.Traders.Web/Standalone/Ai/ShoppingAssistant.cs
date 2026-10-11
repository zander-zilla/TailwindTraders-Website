using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Tailwind.Traders.Web.Standalone.Ai
{
    public class AssistantAnswer
    {
        public string Answer { get; set; }
        public string Model { get; set; }
        public IReadOnlyList<SemanticSearchHit> Products { get; set; }
    }

    /// <summary>
    /// Retrieval-augmented shopping assistant (feature flag Ai:Assistant:Enabled, off by default).
    /// Retrieval = pgvector semantic search; generation = Bedrock Converse when the Bedrock provider is configured,
    /// otherwise an offline templated answer (so the flow can be demoed without AWS).
    /// </summary>
    public class ShoppingAssistant
    {
        private readonly ISemanticSearchService search;
        private readonly AiOptions options;
        private readonly System.IServiceProvider services;

        public ShoppingAssistant(ISemanticSearchService search, IOptions<AiOptions> options, System.IServiceProvider services)
        {
            this.search = search;
            this.options = options.Value;
            this.services = services;
        }

        public async Task<AssistantAnswer> AskAsync(string question, CancellationToken ct = default)
        {
            using var a = AppTelemetry.Source.StartActivity("assistant.ask");
            var found = await search.SearchAsync(question, 5, ct);
            var bedrock = services.GetService<IAmazonBedrockRuntime>();
            if (bedrock == null || !string.Equals(options.EmbeddingProvider, "bedrock", System.StringComparison.OrdinalIgnoreCase))
            {
                var text = found.Results.Count == 0
                    ? "I couldn't find matching products. Try describing what you want to do (e.g. \"cut wood\", \"water the garden\")."
                    : "These products match: " + string.Join("; ", found.Results.Select(p => $"{p.Name} ({p.TypeName}, ${p.Price.ToString("0.00", CultureInfo.InvariantCulture)})")) + ".";
                return new AssistantAnswer { Answer = text, Model = "offline-template", Products = found.Results };
            }

            var catalog = string.Join("\n", found.Results.Select(p => $"- [{p.Id}] {p.Name} | {p.TypeName} | {p.BrandName} | ${p.Price.ToString("0.00", CultureInfo.InvariantCulture)}"));
            using var chat = AppTelemetry.Source.StartActivity($"chat {options.Assistant.ModelId}", ActivityKind.Client);
            chat?.SetTag("gen_ai.operation.name", "chat");
            chat?.SetTag("gen_ai.system", "aws.bedrock");
            chat?.SetTag("gen_ai.request.model", options.Assistant.ModelId);
            var resp = await bedrock.ConverseAsync(new ConverseRequest
            {
                ModelId = options.Assistant.ModelId,
                System = new List<SystemContentBlock> { new() { Text =
                    "You are the Tailwind Traders shopping assistant. Recommend only products from the CATALOG list, " +
                    "cite them by name, keep answers under 80 words, and say so if nothing fits. Never invent prices." } },
                Messages = new List<Message> { new() { Role = ConversationRole.User, Content = new List<ContentBlock> {
                    new() { Text = $"CATALOG:\n{catalog}\n\nQUESTION: {question}" } } } },
                InferenceConfig = new InferenceConfiguration { MaxTokens = options.Assistant.MaxTokens, Temperature = 0.2f },
            }, ct);
            chat?.SetTag("gen_ai.usage.input_tokens", resp.Usage?.InputTokens);
            chat?.SetTag("gen_ai.usage.output_tokens", resp.Usage?.OutputTokens);
            var answer = string.Concat(resp.Output.Message.Content.Select(c => c.Text));
            return new AssistantAnswer { Answer = answer, Model = options.Assistant.ModelId, Products = found.Results };
        }
    }
}
