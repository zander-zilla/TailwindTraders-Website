namespace Tailwind.Traders.Web.Standalone.Ai
{
    /// <summary>Config section "Ai" (env: Ai__EmbeddingProvider=bedrock, Ai__Assistant__Enabled=true, ...).</summary>
    public class AiOptions
    {
        /// <summary>"local" (default; offline, deterministic) or "bedrock" (Amazon Bedrock).</summary>
        public string EmbeddingProvider { get; set; } = "local";
        /// <summary>Region for Bedrock; empty = AWS_REGION / SDK default chain.</summary>
        public string Region { get; set; } = "";
        public string EmbeddingModelId { get; set; } = "amazon.titan-embed-text-v2:0";
        public AssistantOptions Assistant { get; set; } = new();
        /// <summary>Re-embed products during --migrate when missing / text or model changed.</summary>
        public bool EmbedOnMigrate { get; set; } = true;
        /// <summary>Hits below this cosine similarity are dropped. 0.2 suits local-hash-v1 on the demo catalog; tune for Bedrock (e.g. 0.15).</summary>
        public double MinScore { get; set; } = 0.2;
    }

    public class AssistantOptions
    {
        /// <summary>Feature flag: POST /api/v1/assistant returns 404 unless enabled.</summary>
        public bool Enabled { get; set; } = false;
        /// <summary>Bedrock model for the Converse API (used when EmbeddingProvider = bedrock).</summary>
        public string ModelId { get; set; } = "amazon.nova-lite-v1:0";
        public int MaxTokens { get; set; } = 400;
    }
}
