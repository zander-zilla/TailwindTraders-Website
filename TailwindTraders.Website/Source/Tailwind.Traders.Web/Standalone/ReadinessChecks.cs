using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Tailwind.Traders.Web.Standalone.Cart;
using Tailwind.Traders.Web.Standalone.Data;

namespace Tailwind.Traders.Web.Standalone
{
    /// <summary>Readiness (/readyz): catalog DB reachable and migrated. Not used for liveness (a DB outage must not restart pods).</summary>
    public class CatalogDbReadinessCheck : IHealthCheck
    {
        private readonly CatalogDbContext db;
        public CatalogDbReadinessCheck(CatalogDbContext db) => this.db = db;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                if (!await db.Database.CanConnectAsync(ct)) return HealthCheckResult.Unhealthy("PostgreSQL unreachable");
                return await db.Types.AnyAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("catalog not seeded (run --migrate)");
            }
            catch (Exception e)
            {
                return HealthCheckResult.Unhealthy("PostgreSQL check failed", e);
            }
        }
    }

    /// <summary>Readiness (/readyz): DynamoDB cart table exists and is ACTIVE (needs dynamodb:DescribeTable).</summary>
    public class CartTableReadinessCheck : IHealthCheck
    {
        private readonly IAmazonDynamoDB ddb;
        private readonly CartOptions options;
        public CartTableReadinessCheck(IAmazonDynamoDB ddb, IOptions<CartOptions> options) { this.ddb = ddb; this.options = options.Value; }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                var t = await ddb.DescribeTableAsync(options.TableName, ct);
                return t.Table.TableStatus == Amazon.DynamoDBv2.TableStatus.ACTIVE
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy($"table {options.TableName} is {t.Table.TableStatus}");
            }
            catch (Exception e)
            {
                return HealthCheckResult.Unhealthy($"DynamoDB table {options.TableName} not available", e);
            }
        }
    }
}
