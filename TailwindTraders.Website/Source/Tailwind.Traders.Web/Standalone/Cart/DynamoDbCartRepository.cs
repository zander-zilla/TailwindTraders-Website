using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tailwind.Traders.Web.Standalone.Cart
{
    public interface ICartRepository
    {
        Task<IReadOnlyList<CartItem>> GetAsync(string userId, CancellationToken ct = default);
        /// <summary>Adds qty to the line for productId (creates it if missing). Returns the new line.</summary>
        Task<CartItem> AddAsync(string userId, int productId, string name, decimal price, string imageUrl, int qty, CancellationToken ct = default);
        /// <summary>Sets qty on an existing line. False if the line doesn't exist.</summary>
        Task<bool> SetQtyAsync(string userId, int productId, int qty, CancellationToken ct = default);
        /// <summary>Deletes a line. False if it didn't exist.</summary>
        Task<bool> DeleteAsync(string userId, int productId, CancellationToken ct = default);
    }

    /// <summary>
    /// Cart in Amazon DynamoDB (ADR 0008). One item per cart line:
    ///   PK userId (S) = JWT name (email), SK productId (N);
    ///   attributes qty (N), name (S), price (N), imageUrl (S), updatedAt (S, ISO-8601), expiresAt (N, epoch s, TTL).
    /// Access patterns: Query(PK) for the cart; UpdateItem/DeleteItem by (PK, SK). No scans, no GSIs.
    /// </summary>
    public class DynamoDbCartRepository : ICartRepository
    {
        private const string PK = "userId", SK = "productId";
        private readonly IAmazonDynamoDB ddb;
        private readonly CartOptions options;
        private readonly TimeProvider clock;

        public DynamoDbCartRepository(IAmazonDynamoDB ddb, IOptions<CartOptions> options, TimeProvider clock = null)
        {
            this.ddb = ddb;
            this.options = options.Value;
            this.clock = clock ?? TimeProvider.System;
        }

        private static AttributeValue S(string v) => new() { S = v };
        private static AttributeValue N(decimal v) => new() { N = v.ToString(CultureInfo.InvariantCulture) };
        private static Dictionary<string, AttributeValue> Key(string userId, int productId) =>
            new() { [PK] = S(userId), [SK] = N(productId) };

        private (AttributeValue updatedAt, AttributeValue expiresAt) Stamps()
        {
            var now = clock.GetUtcNow();
            return (S(now.ToString("O")), N(now.AddDays(options.TtlDays).ToUnixTimeSeconds()));
        }

        public async Task<IReadOnlyList<CartItem>> GetAsync(string userId, CancellationToken ct = default)
        {
            var items = new List<CartItem>();
            Dictionary<string, AttributeValue> lastKey = null;
            do
            {
                var res = await ddb.QueryAsync(new QueryRequest
                {
                    TableName = options.TableName,
                    KeyConditionExpression = "#pk = :u",
                    ExpressionAttributeNames = new() { ["#pk"] = PK },
                    ExpressionAttributeValues = new() { [":u"] = S(userId) },
                    ConsistentRead = true,
                    ExclusiveStartKey = lastKey,
                }, ct);
                items.AddRange(res.Items.Select(ToItem));
                lastKey = res.LastEvaluatedKey is { Count: > 0 } ? res.LastEvaluatedKey : null;
            } while (lastKey != null);
            return items;
        }

        public async Task<CartItem> AddAsync(string userId, int productId, string name, decimal price, string imageUrl, int qty, CancellationToken ct = default)
        {
            var (updatedAt, expiresAt) = Stamps();
            var res = await ddb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = options.TableName,
                Key = Key(userId, productId),
                // ADD is atomic: two concurrent "add to cart" clicks end up as one line with qty 2.
                UpdateExpression = "SET #n = :n, price = :p, imageUrl = :i, updatedAt = :ua, expiresAt = :ea ADD qty :q",
                ExpressionAttributeNames = new() { ["#n"] = "name" }, // reserved word
                ExpressionAttributeValues = new()
                {
                    [":n"] = S(name ?? ""), [":p"] = N(price), [":i"] = S(imageUrl ?? ""),
                    [":ua"] = updatedAt, [":ea"] = expiresAt, [":q"] = N(qty),
                },
                ReturnValues = ReturnValue.ALL_NEW,
            }, ct);
            return ToItem(res.Attributes);
        }

        public async Task<bool> SetQtyAsync(string userId, int productId, int qty, CancellationToken ct = default)
        {
            var (updatedAt, expiresAt) = Stamps();
            try
            {
                await ddb.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = options.TableName,
                    Key = Key(userId, productId),
                    UpdateExpression = "SET qty = :q, updatedAt = :ua, expiresAt = :ea",
                    ConditionExpression = "attribute_exists(#pk)", // never create a line from an update
                    ExpressionAttributeNames = new() { ["#pk"] = PK },
                    ExpressionAttributeValues = new() { [":q"] = N(qty), [":ua"] = updatedAt, [":ea"] = expiresAt },
                }, ct);
                return true;
            }
            catch (ConditionalCheckFailedException)
            {
                return false;
            }
        }

        public async Task<bool> DeleteAsync(string userId, int productId, CancellationToken ct = default)
        {
            var res = await ddb.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = options.TableName,
                Key = Key(userId, productId),
                ReturnValues = ReturnValue.ALL_OLD,
            }, ct);
            return res.Attributes is { Count: > 0 };
        }

        private static CartItem ToItem(Dictionary<string, AttributeValue> a) => new()
        {
            Email = a[PK].S,
            Id = int.Parse(a[SK].N, CultureInfo.InvariantCulture),
            Name = a.TryGetValue("name", out var n) ? n.S : null,
            ImageUrl = a.TryGetValue("imageUrl", out var i) ? i.S : null,
            Price = a.TryGetValue("price", out var p) ? decimal.Parse(p.N, CultureInfo.InvariantCulture) : 0m,
            Qty = a.TryGetValue("qty", out var q) ? int.Parse(q.N, CultureInfo.InvariantCulture) : 0,
        };
    }
}
