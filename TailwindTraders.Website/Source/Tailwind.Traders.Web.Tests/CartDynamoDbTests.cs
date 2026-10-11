using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tailwind.Traders.Web.Standalone.Cart;
using Xunit;

namespace Tailwind.Traders.Web.Tests
{
    /// <summary>Phase 3b: DynamoDB cart (table design, isolation, TTL attribute, idempotent table creation).
    /// Note: DynamoDB Local stores the TTL setting but never deletes expired items, so expiry itself is not tested.</summary>
    [Collection("standalone")]
    public class CartDynamoDbTests
    {
        private readonly StandaloneFixture _fx;
        public CartDynamoDbTests(StandaloneFixture fx) => _fx = fx;

        private static StringContent Body(string json) => new(json, Encoding.UTF8, "application/json");

        private async Task<HttpClient> LoginAs(string user)
        {
            var c = _fx.Factory.CreateClient();
            var login = await c.PostAsync("/api/v1/login", Body($"{{\"username\":\"{user}\",\"password\":\"x\",\"grant_type\":\"password\"}}"));
            var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("access_token").GetProperty("token").GetString();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return c;
        }

        [Fact]
        public async Task Carts_are_isolated_per_user()
        {
            var a = await LoginAs("alice-3b@contoso.com");
            var b = await LoginAs("bob-3b@contoso.com");
            Assert.Equal(HttpStatusCode.Created, (await a.PostAsync("/api/v1/shoppingcart", Body("{\"detailProduct\":{\"id\":2},\"qty\":1}"))).StatusCode);
            var bobCart = JsonDocument.Parse(await b.GetStringAsync("/api/v1/shoppingcart")).RootElement;
            Assert.Equal(0, bobCart.GetArrayLength());
            // Bob can't delete Alice's line.
            var del = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/shoppingcart/product") { Content = Body("{\"id\":\"2\"}") };
            Assert.Equal(HttpStatusCode.NotFound, (await b.SendAsync(del)).StatusCode);
            Assert.Equal(1, JsonDocument.Parse(await a.GetStringAsync("/api/v1/shoppingcart")).RootElement.GetArrayLength());
        }

        [Fact]
        public async Task Unknown_product_is_rejected_and_bad_ids_are_404()
        {
            var c = await LoginAs("carol-3b@contoso.com");
            Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync("/api/v1/shoppingcart", Body("{\"detailProduct\":{\"id\":99999},\"qty\":1}"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync("/api/v1/shoppingcart/product", Body("{\"id\":\"6b1f0a7e-legacy-guid\",\"qty\":2}"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/api/v1/shoppingcart/product", Body("{\"id\":\"1\",\"qty\":0}"))).StatusCode);
        }

        [Fact]
        public async Task Item_layout_has_keys_qty_and_ttl()
        {
            var c = await LoginAs("dave-3b@contoso.com");
            await c.PostAsync("/api/v1/shoppingcart", Body("{\"detailProduct\":{\"id\":3},\"qty\":2}"));

            var ddb = _fx.Factory.Services.GetRequiredService<IAmazonDynamoDB>();
            var table = _fx.Factory.Services.GetRequiredService<IOptions<CartOptions>>().Value.TableName;
            var item = (await ddb.GetItemAsync(table, new()
            {
                ["userId"] = new AttributeValue { S = "dave-3b@contoso.com" },
                ["productId"] = new AttributeValue { N = "3" },
            })).Item;
            Assert.Equal("2", item["qty"].N);
            Assert.True(DateTimeOffset.TryParse(item["updatedAt"].S, out _));
            var expires = DateTimeOffset.FromUnixTimeSeconds(long.Parse(item["expiresAt"].N));
            Assert.InRange(expires, DateTimeOffset.UtcNow.AddDays(29), DateTimeOffset.UtcNow.AddDays(31));

            var desc = (await ddb.DescribeTableAsync(table)).Table;
            Assert.Equal(BillingMode.PAY_PER_REQUEST, desc.BillingModeSummary?.BillingMode ?? BillingMode.PAY_PER_REQUEST);
            Assert.Equal(new[] { "userId", "productId" }, desc.KeySchema.Select(k => k.AttributeName));
            var ttl = await ddb.DescribeTimeToLiveAsync(new DescribeTimeToLiveRequest { TableName = table });
            Assert.Equal("expiresAt", ttl.TimeToLiveDescription.AttributeName);
        }

        [Fact]
        public async Task Table_creation_is_idempotent_and_skippable()
        {
            var ddb = _fx.Factory.Services.GetRequiredService<IAmazonDynamoDB>();
            var opts = _fx.Factory.Services.GetRequiredService<IOptions<CartOptions>>();
            await new CartTableInitializer(ddb, opts, NullLogger.Instance).EnsureTableAsync(); // second run: no throw
            var skip = Options.Create(new CartOptions { TableName = "does-not-exist", CreateTable = false });
            await new CartTableInitializer(ddb, skip, NullLogger.Instance).EnsureTableAsync();
            var tables = (await ddb.ListTablesAsync()).TableNames;
            Assert.DoesNotContain("does-not-exist", tables);
        }
    }
}
