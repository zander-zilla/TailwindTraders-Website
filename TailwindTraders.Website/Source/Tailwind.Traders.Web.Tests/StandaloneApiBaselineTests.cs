using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Tailwind.Traders.Web.Tests
{
    /// <summary>
    /// Characterization ("golden") tests: they pin down what the legacy Standalone API returns today,
    /// so every modernization phase (.NET 10, PostgreSQL/EF Core, DynamoDB) can prove behavioral parity.
    /// </summary>
    [Collection("standalone")]
    public class StandaloneApiBaselineTests
    {
        private readonly StandaloneFixture _fx;
        public StandaloneApiBaselineTests(StandaloneFixture fx) => _fx = fx;

        private static async Task<JsonElement> Json(HttpResponseMessage r)
        {
            var body = await r.Content.ReadAsStringAsync();
            Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode}: {body}");
            return JsonDocument.Parse(body).RootElement;
        }

        private static StringContent Body(string json) => new StringContent(json, Encoding.UTF8, "application/json");

        [Fact]
        public async Task Settings_returns_frontend_configuration()
        {
            var json = await Json(await _fx.Factory.CreateClient().GetAsync("/api/settings"));
            Assert.Equal(JsonValueKind.Object, json.ValueKind);
            Assert.True(json.TryGetProperty("apiUrl", out _), json.ToString());
            // Phase 1 fix (legacy bug 3): Newtonsoft [JsonIgnore] was ignored by System.Text.Json, so the
            // .NET 5 app leaked SqlConnectionString / MongoConnectionString (with passwords) to the browser.
            Assert.False(json.TryGetProperty("sqlConnectionString", out _), "SQL connection string leaked");
            Assert.False(json.TryGetProperty("mongoConnectionString", out _), "Mongo connection string leaked");
        }

        [Fact]
        public async Task Healthz_liveness_returns_healthy()
        {
            var r = await _fx.Factory.CreateClient().GetAsync("/healthz");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("Healthy", await r.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Products_list_returns_seeded_catalog()
        {
            var json = await Json(await _fx.Factory.CreateClient().GetAsync("/api/v1/products"));
            Assert.Equal(Baseline.BrandCount, json.GetProperty("brands").GetArrayLength());
            Assert.Equal(Baseline.TypeCount, json.GetProperty("types").GetArrayLength());
            Assert.Equal(Baseline.UnfilteredProductCount, json.GetProperty("products").GetArrayLength());
        }

        [Fact]
        public async Task Products_can_be_filtered_by_type()
        {
            var json = await Json(await _fx.Factory.CreateClient().GetAsync($"/api/v1/products?type={Baseline.FilterTypeCode}"));
            var products = json.GetProperty("products").EnumerateArray().ToList();
            Assert.Equal(Baseline.FilterTypeProductCount, products.Count);
            Assert.All(products, p => Assert.Equal(Baseline.FilterTypeCode, p.GetProperty("type").GetProperty("code").GetString()));
        }

        [Fact]
        public async Task Product_detail_returns_product_with_brand_type_and_features()
        {
            var json = await Json(await _fx.Factory.CreateClient().GetAsync("/api/v1/products/1"));
            Assert.Equal(1, json.GetProperty("id").GetInt32());
            Assert.Equal(Baseline.Product1Name, json.GetProperty("name").GetString());
            Assert.Equal(Baseline.Product1Price, json.GetProperty("price").GetDecimal());
            Assert.Equal(JsonValueKind.Object, json.GetProperty("brand").ValueKind);
            Assert.Equal(JsonValueKind.Array, json.GetProperty("features").ValueKind);
        }

        /// <summary>Status code, treating an unhandled server exception (TestServer rethrows) as 500.</summary>
        private static async Task<HttpStatusCode> StatusOf(HttpClient c, HttpRequestMessage req)
        {
            try { return (await c.SendAsync(req)).StatusCode; }
            catch (System.InvalidOperationException) { return HttpStatusCode.InternalServerError; }
        }

        [Fact]
        public async Task Landing_returns_empty_array()
        {
            var c = _fx.Factory.CreateClient();
            Assert.Equal(0, (await Json(await c.GetAsync("/api/v1/products/landing"))).GetArrayLength());
        }

        [Theory]
        [InlineData("/api/v1/profiles/navbar/me")]
        [InlineData("/api/v1/shoppingcart")]
        [InlineData("/api/v1/shoppingcart/relatedproducts")]
        public async Task Authorized_endpoints_status(string url)
        {
            var status = await StatusOf(_fx.Factory.CreateClient(), new HttpRequestMessage(HttpMethod.Get, url));
            Assert.Equal(Baseline.AuthorizedEndpointAnonymousStatus, status);
        }

        [Fact]
        public async Task Login_rejects_missing_credentials()
        {
            var r = await _fx.Factory.CreateClient().PostAsync("/api/v1/login", Body("{}"));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        [Fact]
        public async Task Login_and_shopping_cart_round_trip()
        {
            var c = _fx.Factory.CreateClient();
            var login = await c.PostAsync("/api/v1/login",
                Body("{\"username\":\"baseline@contoso.com\",\"password\":\"x\",\"grant_type\":\"password\"}"));
            Assert.True(Baseline.LoginStatus == login.StatusCode, $"{login.StatusCode}: {await login.Content.ReadAsStringAsync()}");

            var token = (await Json(login)).GetProperty("access_token").GetProperty("token").GetString();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var add = await c.PostAsync("/api/v1/shoppingcart",
                Body("{\"detailProduct\":{\"id\":1,\"name\":\"P1\",\"imageUrl\":\"p1.jpg\",\"price\":10.5},\"qty\":2}"));
            Assert.Equal(HttpStatusCode.Created, add.StatusCode);
            var guid = (await Json(add)).GetProperty("id").GetString();

            var cart = (await Json(await c.GetAsync("/api/v1/shoppingcart"))).EnumerateArray().ToList();
            var item = Assert.Single(cart, i => i.GetProperty(Baseline.CartItemIdProperty).GetString() == guid);
            Assert.Equal(2, item.GetProperty("qty").GetInt32());

            var upd = await c.PostAsync("/api/v1/shoppingcart/product", Body($"{{\"id\":\"{guid}\",\"qty\":5}}"));
            Assert.Equal(HttpStatusCode.Created, upd.StatusCode);
            cart = (await Json(await c.GetAsync("/api/v1/shoppingcart"))).EnumerateArray().ToList();
            Assert.Equal(5, cart.Single(i => i.GetProperty(Baseline.CartItemIdProperty).GetString() == guid).GetProperty("qty").GetInt32());

            var del = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/shoppingcart/product") { Content = Body($"{{\"id\":\"{guid}\"}}") };
            Assert.Equal(HttpStatusCode.OK, (await c.SendAsync(del)).StatusCode);
            cart = (await Json(await c.GetAsync("/api/v1/shoppingcart"))).EnumerateArray().ToList();
            Assert.DoesNotContain(cart, i => i.GetProperty(Baseline.CartItemIdProperty).GetString() == guid);
        }
    }

    /// <summary>Golden values captured from the legacy .NET 5 app (seed CSVs from TailwindTraders-Backend).</summary>
    internal static class Baseline
    {
        public const int BrandCount = 4;
        public const int TypeCount = 7;
        public const int SeededProductCount = 70;
        // Quirk: with no brand/type filter the WHERE clause (IN @TypeCodes OR IN @BrandIds OR LIKE NULL)
        // matches nothing, so the unfiltered list returns 0 products. The UI always filters by type.
        public const int UnfilteredProductCount = 0;
        public const string FilterTypeCode = "home";
        public const int FilterTypeProductCount = 8;
        public const string Product1Name = "Microwave 0.9 Cu. Ft. 900 W";
        public const decimal Product1Price = 100.00m;

        // ---- Legacy bugs found in Phase 0, FIXED in Phase 1 (.NET 10) ----
        // 1) Was 500: Startup never called app.UseAuthorization(). Now anonymous callers get 401.
        public const HttpStatusCode AuthorizedEndpointAnonymousStatus = HttpStatusCode.Unauthorized;
        // 2) Was 400: Newtonsoft [JsonProperty("grant_type")] ignored by System.Text.Json. Now [JsonPropertyName].
        public const HttpStatusCode LoginStatus = HttpStatusCode.OK;
        // 3) Settings leaked DB connection strings (Newtonsoft [JsonIgnore] ignored) — asserted in Settings test.
        // 4) Cart items were serialized as "guid" but the React UI reads "_cdbid" (Newtonsoft attribute ignored),
        //    so quantity update/delete from the UI never matched. Now "_cdbid" as the UI expects.
        public const string CartItemIdProperty = "_cdbid";
    }
}
