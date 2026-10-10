using System.Text.Json.Serialization;

namespace Tailwind.Traders.Web.Standalone.Models
{
    public class AccessToken
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}