# 0008. Amazon DynamoDB for the shopping cart

- **Status:** Accepted (implemented in Phase 3b)
- **Date:** 2026-10-10

## Context
The cart lives in MongoDB / Cosmos DB (Mongo API) via `MongoDB.Driver`, and the frontend also contains a direct `@azure/cosmos` cart DAO. Carts are small, key-scoped, short-lived documents with no cross-cart queries. Alternatives considered: PostgreSQL `jsonb`, which means one database but couples cart load to the catalog DB, and Amazon DocumentDB, which is Mongo-compatible but always-on instance cost.

## Decision
Use **Amazon DynamoDB** through `AWSSDK.DynamoDBv2`:
- Table `tt-cart`, partition key `cartId` (user id or anonymous session id), sort key `productId`.
- **TTL** attribute `expiresAt` for abandoned carts, refreshed on write. **On-demand** capacity.
- Access via **EKS Pod Identity** with a least-privilege table policy. No access keys.
- Locally, **DynamoDB Local or LocalStack**, with a `ServiceURL` override.
- Remove `MongoDB.Driver`, `MongoConnectionString` and the frontend Cosmos DAO; all cart access goes through the API.

## Consequences
- Serverless, pay-per-request, zero-ops cart store with automatic expiry, which makes a good demo story.
- Existing Mongo carts are not migrated (they are ephemeral).
- TTL is not enforced by DynamoDB Local, so show expiry on AWS. Pod Identity/IAM misconfiguration is a common first-deploy failure.
- Adds an AWS-specific dependency to application code, which is accepted.

## Implementation notes (Phase 3b)
- **Table** `tt-cart` (config `Cart:TableName`): PK `userId` (S, the JWT name/email), SK `productId` (N). Attributes `qty` (N), `name`, `price` (N), `imageUrl`, `updatedAt` (ISO-8601), `expiresAt` (N, epoch seconds, **TTL**, `Cart:TtlDays` = 30 after the last change). On-demand capacity, no GSIs.
- **Access patterns:** get cart = `Query(userId)` (consistent read, paginated). Add = `UpdateItem … ADD qty :q` (atomic; duplicates merge). Update = `UpdateItem` with `attribute_exists` → **404** if missing. Delete = `DeleteItem ReturnValues=ALL_OLD` → **404** if nothing deleted.
- **Prices from the catalog:** POST looks up the product in PostgreSQL. The client's `name`/`price`/`imageUrl` are ignored, and unknown products get 404.
- **SPA compatibility:** the JSON shape is unchanged. `_cdbid` is now the product id as a string, and the UI's update/delete call is wrapped so a 404 can't wedge the cart page.
- **Config:** `Cart:ServiceUrl` (DynamoDB Local endpoint, with dummy keys if none are set), `Cart:Region` / `AWS_REGION`, and `Cart:CreateTable` (the `--migrate` step creates the table and enables TTL idempotently; set **false in AWS**, where IaC owns the table). Credentials use the AWS SDK default chain (EKS Pod Identity in Phase 4).
- **Removed:** MongoDB.Driver, `MongoConnectionString`, the browser Cosmos DB DAO (`@azure/cosmos`), `ByPassShoppingCartApi`, and the `Cart` settings block that sent a **Cosmos master key (`Cart.AuthKey`) to the browser** via `/api/settings`.
- **Local caveat:** DynamoDB Local stores the TTL setting but never expires items. Compose runs it in memory, so carts reset when the container restarts.
