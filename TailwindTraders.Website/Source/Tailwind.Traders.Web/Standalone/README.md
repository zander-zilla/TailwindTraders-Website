# Standalone mode

The website hosts its own product, cart, login and image-search APIs (no TailwindTraders-Backend needed).

| Concern | Store | Config |
|---|---|---|
| Product catalog | PostgreSQL (EF Core 10 + Npgsql). Aurora PostgreSQL in AWS | `ConnectionStrings__Products` |
| Shopping cart | Amazon DynamoDB (DynamoDB Local for development) | `Cart__TableName` (default `tt-cart`), `Cart__ServiceUrl` (local endpoint only), `Cart__Region` / `AWS_REGION`, `Cart__CreateTable` (`false` in AWS), `Cart__TtlDays` |
| Image search | Embedded ONNX model (or `ImagePredictorEndpoint`) | — |
| Login | Demo JWT (any user/password) | `SecurityKey` |

Standalone mode is enabled when `ConnectionStrings__Products` is set.

**Schema and data:** run `dotnet Tailwind.Traders.Web.dll --migrate` once per deploy. It applies EF Core migrations, seeds the catalog from `Data/Seed/*.csv` (idempotent) and, if `Cart__CreateTable=true`, creates the DynamoDB table with TTL on `expiresAt`. In AWS the cart table is created by IaC and the app's IAM role (EKS Pod Identity) needs `dynamodb:Query/GetItem/UpdateItem/DeleteItem` on it.

Local full stack: `docker compose -f ../docker-compose.standalone.yml up -d --build`. See also [docs/modernization](../../../../docs/modernization/README.md).
