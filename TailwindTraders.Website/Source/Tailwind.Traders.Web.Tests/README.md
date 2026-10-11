# Tailwind.Traders.Web.Tests — baseline (characterization) tests

Phase 0 of the modernization plan. These xUnit tests boot the website in-process with
`WebApplicationFactory<Startup>` in **Standalone mode** (in-process product/cart/login APIs backed by
PostgreSQL 17 (EF Core, since Phase 3a) + DynamoDB Local (cart, since Phase 3b)) and pin down what the legacy app returns today. Later phases (.NET 10,
PostgreSQL + EF Core, DynamoDB) must keep them green — or consciously change a pinned value.

## Prerequisites
- .NET SDK 10 (as of Phase 1 both projects target `net10.0`; the Phase 0 branch needed the .NET 5 runtime + `libssl1.1`).
- Docker (Testcontainers starts `postgres:17` and `amazon/dynamodb-local`).
- No internet access needed for data: seed CSVs are bundled (`Standalone/Data/Seed`).

## Run
```bash
cd TailwindTraders.Website/Source
dotnet test Tailwind.Traders.Web.Tests -p:BuildingDocker=true   # BuildingDocker skips the npm install/build hooks
```
To reuse already-running databases instead of Testcontainers:
```bash
export TT_PG_CONNECTION='Host=localhost;Database=tailwind;Username=tailwind;Password=...'
export TT_DYNAMODB_URL='http://localhost:8000'
```

## What is pinned (golden values)
| Area | Baseline |
|---|---|
| Seed data | 4 brands, 7 types, 70 products; product 1 = "Microwave 0.9 Cu. Ft. 900 W", 100.00; `type=home` → 8 products |
| `/api/settings` | 200, JSON config for the SPA |
| `/api/v1/products` (no filter) | 200, **0 products** (WHERE clause quirk — UI always filters) |
| `/api/v1/products/landing` | `[]` |
| Legacy bug 1 (fixed Phase 1) | No `app.UseAuthorization()` → `[Authorize]` endpoints returned **500**; now **401** for anonymous |
| Legacy bug 2 (fixed Phase 1) | Newtonsoft `[JsonProperty("grant_type")]` ignored by System.Text.Json → login always **400**; now **200** + JWT |
| Legacy bug 3 (fixed Phase 1) | Newtonsoft `[JsonIgnore]` ignored → `/api/settings` **leaked SQL/Mongo connection strings** to the browser |
| Legacy bug 4 (fixed Phase 1) | Cart items serialized as `guid` but the UI reads `_cdbid` → UI qty update/delete never matched |
| Cart | login → add → get → update qty → delete round trip (runs since Phase 1) |
| Image search | embedded ONNX model classifies an uploaded image (200/404, never 500) |
| DynamoDB cart (Phase 3b) | adds merge into one line per product (qty 2+1 → 3), name/price come from the catalog (not the client), 404 for missing lines/unknown products, 400 for qty < 1, per-user isolation, item layout (keys, qty, updatedAt, expiresAt ≈ +30 d), table on-demand + TTL on `expiresAt`, idempotent/skippable table creation, no `cart` block in `/api/settings`. **TTL expiry itself isn't testable: DynamoDB Local doesn't delete expired items.** |
| PostgreSQL (Phase 3a) | name search case-insensitive (`microwave`/`MICROWAVE`), `%`/`_` literal, type filter case-insensitive, seeding idempotent + repairs partial seed, no pending migrations, no `connectionStrings` in `/api/settings` |
