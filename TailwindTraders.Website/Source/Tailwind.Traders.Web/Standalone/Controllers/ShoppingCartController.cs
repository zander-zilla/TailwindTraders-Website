using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Infrastructure;
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

        // Span + counter per cart operation. User id is NOT recorded (PII); product id and qty are.
        private static System.Diagnostics.Activity StartOp(string op) =>
            AppTelemetry.Source.StartActivity("cart." + op)?.SetTag("tailwindtraders.cart.operation", op);

        private static IActionResult Done(System.Diagnostics.Activity activity, string op, IActionResult result)
        {
            var status = (result as IStatusCodeActionResult)?.StatusCode ?? 200;
            var outcome = status switch { < 400 => "ok", 404 => "not_found", 400 => "bad_request", _ => "error" };
            activity?.SetTag("tailwindtraders.cart.result", outcome);
            if (status >= 500) activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
            AppTelemetry.CartOperations.Add(1,
                new KeyValuePair<string, object>("operation", op), new KeyValuePair<string, object>("result", outcome));
            return result;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            using var a = StartOp("get");
            var items = await cart.GetAsync(UserId);
            a?.SetTag("tailwindtraders.cart.lines", items.Count);
            return Done(a, "get", Ok(items));
        }

        [HttpPost]
        public async Task<IActionResult> PostAsync([FromBody] ShoppingCartItemRequest itemRequest)
        {
            using var a = StartOp("add");
            if (itemRequest?.DetailProduct == null) return Done(a, "add", BadRequest());
            var qty = itemRequest.Qty < 1 ? 1 : itemRequest.Qty;
            a?.SetTag("tailwindtraders.product.id", itemRequest.DetailProduct.Id);
            a?.SetTag("tailwindtraders.cart.qty", qty);

            // Name, price and image come from the catalog (PostgreSQL). Client-sent values are ignored.
            var product = await products.GetProduct(itemRequest.DetailProduct.Id);
            if (product == null) return Done(a, "add", NotFound(new { message = "Unknown product" }));

            var line = await cart.AddAsync(UserId, product.Id, product.Name, product.Price, product.ImageUrl, qty);

            AppTelemetry.CartUnitsAdded.Add(qty);
            a?.SetTag("tailwindtraders.cart.line_qty", line.Qty);
            return Done(a, "add", Created("/api/v1/shoppingcart", new
            {
                message = "Product added to shopping cart",
                id = line.LineId
            }));
        }

        [HttpPost("product")]
        public async Task<IActionResult> UpdateProductAsync([FromBody] ShoppingCartItemUpdateRequest updateRequest)
        {
            using var a = StartOp("update");
            if (updateRequest == null || updateRequest.Qty < 1) return Done(a, "update", BadRequest(new { message = "qty must be >= 1; use DELETE to remove" }));
            a?.SetTag("tailwindtraders.cart.qty", updateRequest.Qty);
            if (!TryParseLine(updateRequest.Id, out var productId) || !await cart.SetQtyAsync(UserId, productId, updateRequest.Qty))
                return Done(a, "update", NotFound());

            // 201 kept to match the existing API contract.
            return Done(a, "update", Created("/api/v1/shoppingcart", new { Message = "Product qty updated" }));
        }

        [HttpDelete("product")]
        public async Task<IActionResult> DeleteProductAsync([FromBody] ShoppingCartItemDeleteRequest deleteRequest)
        {
            using var a = StartOp("delete");
            if (!TryParseLine(deleteRequest?.Id, out var productId) || !await cart.DeleteAsync(UserId, productId))
                return Done(a, "delete", NotFound());
            return Done(a, "delete", Ok());
        }

        [HttpGet("relatedproducts")]
        public IActionResult GetRelatedAsync() => Ok(new object[] { });

        private static bool TryParseLine(string id, out int productId) =>
            int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out productId);
    }
}
