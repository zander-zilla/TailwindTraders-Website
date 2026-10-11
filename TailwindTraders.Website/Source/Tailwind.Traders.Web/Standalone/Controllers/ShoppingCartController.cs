using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tailwind.Traders.Web.Standalone.Cart;
using Tailwind.Traders.Web.Standalone.Models;

namespace Tailwind.Traders.Web.Standalone.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class ShoppingCartController : Controller
    {
        private readonly ICartRepository cart;
        private readonly IProductService products;

        public ShoppingCartController(ICartRepository cart, IProductService products)
        {
            this.cart = cart;
            this.products = products;
        }

        private string UserId => User.Identity.Name;

        [HttpGet]
        public async Task<IActionResult> GetAsync() => Ok(await cart.GetAsync(UserId));

        [HttpPost]
        public async Task<IActionResult> PostAsync([FromBody] ShoppingCartItemRequest itemRequest)
        {
            if (itemRequest?.DetailProduct == null) return BadRequest();
            var qty = itemRequest.Qty < 1 ? 1 : itemRequest.Qty;

            // Name, price and image come from the catalog (PostgreSQL). Client-sent values are ignored.
            var product = await products.GetProduct(itemRequest.DetailProduct.Id);
            if (product == null) return NotFound(new { message = "Unknown product" });

            var line = await cart.AddAsync(UserId, product.Id, product.Name, product.Price, product.ImageUrl, qty);

            return Created("/api/v1/shoppingcart", new
            {
                message = "Product added to shopping cart",
                id = line.LineId
            });
        }

        [HttpPost("product")]
        public async Task<IActionResult> UpdateProductAsync([FromBody] ShoppingCartItemUpdateRequest updateRequest)
        {
            if (updateRequest == null || updateRequest.Qty < 1) return BadRequest(new { message = "qty must be >= 1; use DELETE to remove" });
            if (!TryParseLine(updateRequest.Id, out var productId) || !await cart.SetQtyAsync(UserId, productId, updateRequest.Qty))
                return NotFound();

            // 201 kept to match the existing API contract.
            return Created("/api/v1/shoppingcart", new { Message = "Product qty updated" });
        }

        [HttpDelete("product")]
        public async Task<IActionResult> DeleteProductAsync([FromBody] ShoppingCartItemDeleteRequest deleteRequest)
        {
            if (!TryParseLine(deleteRequest?.Id, out var productId) || !await cart.DeleteAsync(UserId, productId))
                return NotFound();
            return Ok();
        }

        [HttpGet("relatedproducts")]
        public IActionResult GetRelatedAsync() => Ok(new object[] { });

        private static bool TryParseLine(string id, out int productId) =>
            int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out productId);
    }
}
