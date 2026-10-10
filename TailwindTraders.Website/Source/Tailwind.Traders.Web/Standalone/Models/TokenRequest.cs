using System.Text.Json.Serialization;

namespace Tailwind.Traders.Web.Standalone.Models
{
    public class TokenRequest
    {
        public string Username { get; set; }

        public string Password { get; set; }
        
        [JsonPropertyName("grant_type")]
        public string GrantType { get; set; }
    }
}