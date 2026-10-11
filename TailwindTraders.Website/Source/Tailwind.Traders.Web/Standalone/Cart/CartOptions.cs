namespace Tailwind.Traders.Web.Standalone.Cart
{
    /// <summary>Config section "Cart" (env Cart__TableName, Cart__ServiceUrl, ...). Credentials come from the AWS SDK default chain.</summary>
    public class CartOptions
    {
        public string TableName { get; set; } = "tt-cart";
        /// <summary>Endpoint override for DynamoDB Local / LocalStack (e.g. http://dynamodb:8000). Leave empty in AWS.</summary>
        public string ServiceUrl { get; set; }
        /// <summary>AWS region (falls back to AWS_REGION / SDK chain, then us-east-1).</summary>
        public string Region { get; set; }
        /// <summary>Create the table (idempotently) in the --migrate step. Set false in AWS: the table is owned by IaC.</summary>
        public bool CreateTable { get; set; } = true;
        /// <summary>Abandoned-cart TTL: expiresAt = last change + TtlDays.</summary>
        public int TtlDays { get; set; } = 30;
    }
}
