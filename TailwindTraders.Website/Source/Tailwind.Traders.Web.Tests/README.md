# Tailwind.Traders.Web.Tests — baseline (characterization) tests

Phase 0 of the modernization plan. These xUnit tests boot the website in-process with
`WebApplicationFactory<Startup>` in **Standalone mode** (in-process product/cart/login APIs backed by
SQL Server + MongoDB) and pin down what the legacy app returns today. Later phases (.NET 10,
PostgreSQL + EF Core, DynamoDB) must keep them green — or consciously change a pinned value.

## Prerequisites
- .NET SDK 10 (as of Phase 1 both projects target `net10.0`; the Phase 0 branch needed the .NET 5 runtime + `libssl1.1`).
- Docker (Testcontainers starts `mcr.microsoft.com/mssql/server:2022-latest` and `mongo:7`).
- Internet access: on first start the app seeds SQL from CSVs in the TailwindTraders-Backend GitHub repo.

## Run
```bash
cd TailwindTraders.Website/Source
dotnet test Tailwind.Traders.Web.Tests -p:BuildingDocker=true   # BuildingDocker skips the npm install/build hooks
```
To reuse already-running databases instead of Testcontainers:
```bash
export TT_SQL_CONNECTION='Server=localhost,1433;Database=tailwind;User Id=sa;Password=...;TrustServerCertificate=true'
export TT_MONGO_CONNECTION='mongodb://localhost:27017'
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
