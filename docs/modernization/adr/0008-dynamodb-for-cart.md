# 0008. Amazon DynamoDB for the shopping cart

- **Status:** Accepted (implementation planned for Phase 3b)
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
