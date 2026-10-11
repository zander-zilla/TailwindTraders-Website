using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tailwind.Traders.Web.Standalone.Cart
{
    /// <summary>
    /// Idempotently creates the cart table (on-demand capacity, TTL on expiresAt). Runs in the `--migrate` step
    /// for local/dev only. In AWS set Cart:CreateTable=false: the table is owned by IaC (Phase 5).
    /// Note: DynamoDB Local accepts the TTL setting but does NOT delete expired items.
    /// </summary>
    public class CartTableInitializer
    {
        private readonly IAmazonDynamoDB ddb;
        private readonly CartOptions options;
        private readonly ILogger logger;

        public CartTableInitializer(IAmazonDynamoDB ddb, IOptions<CartOptions> options, ILogger logger)
        {
            this.ddb = ddb; this.options = options.Value; this.logger = logger;
        }

        public async Task EnsureTableAsync(CancellationToken ct = default)
        {
            if (!options.CreateTable)
            {
                logger.LogInformation("Cart:CreateTable=false, so DynamoDB table {Table} is expected to exist (IaC).", options.TableName);
                return;
            }
            try
            {
                await ddb.CreateTableAsync(new CreateTableRequest
                {
                    TableName = options.TableName,
                    BillingMode = BillingMode.PAY_PER_REQUEST,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new("userId", ScalarAttributeType.S),
                        new("productId", ScalarAttributeType.N),
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new("userId", KeyType.HASH),
                        new("productId", KeyType.RANGE),
                    },
                }, ct);
                logger.LogInformation("Created DynamoDB table {Table}.", options.TableName);
            }
            catch (ResourceInUseException)
            {
                logger.LogInformation("DynamoDB table {Table} already exists.", options.TableName);
            }

            for (var i = 0; i < 60; i++)
            {
                var d = await ddb.DescribeTableAsync(options.TableName, ct);
                if (d.Table.TableStatus == TableStatus.ACTIVE) break;
                await Task.Delay(1000, ct);
            }

            var ttl = await ddb.DescribeTimeToLiveAsync(new DescribeTimeToLiveRequest { TableName = options.TableName }, ct);
            if (ttl.TimeToLiveDescription?.TimeToLiveStatus != TimeToLiveStatus.ENABLED)
            {
                await ddb.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
                {
                    TableName = options.TableName,
                    TimeToLiveSpecification = new TimeToLiveSpecification { AttributeName = "expiresAt", Enabled = true },
                }, ct);
                logger.LogInformation("Enabled TTL on {Table}.expiresAt.", options.TableName);
            }
        }
    }
}
