using System.Text.Json.Serialization;

namespace Tailwind.Traders.Web.Standalone.Cart
{
    /// <summary>Cart line returned to the SPA. JSON shape matches the legacy Mongo document.</summary>
    public class CartItem
    {
        /// <summary>Product id.</summary>
        public int Id { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public decimal Price { get; set; }
        public int Qty { get; set; }
        /// <summary>Line id used by the SPA for update/delete. Since Phase 3b it is the product id (sort key) as a string.</summary>
        [JsonPropertyName("_cdbid")]
        public string LineId => Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
