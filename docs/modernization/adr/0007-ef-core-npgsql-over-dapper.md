# 0007. EF Core 10 + Npgsql instead of Dapper

- **Status:** Accepted (implemented in Phase 3a)
- **Date:** 2026-10-10

## Context
Catalog data access uses Dapper with hand-written SQL Server SQL (multi-map JOINs, `IN @list`, `LIKE`). The schema is created by T-SQL `CREATE TABLE` strings at app startup, with no migrations or versioning. The database moves to PostgreSQL ([ADR 0006](0006-target-aws.md)), so all SQL must be rewritten anyway.

Options:
- **A. Keep Dapper**, switch to Npgsql and hand-port SQL, adding DbUp/Flyway: 1–2 days, smallest diff, SQL stays dialect-bound.
- **B. EF Core 10 + Npgsql provider**: code-first model and migrations, LINQ queries: 2–3.5 days.

## Decision
**Option B.** Model the 5 catalog tables as entities with snake_case naming. Generate migrations and ship them as an EF **migration bundle** run by a Helm hook Job, not at app startup. Rewrite `ProductService` in LINQ, using `EF.Functions.ILike` to keep SQL Server's case-insensitive search. Vendor the seed CSVs into the repo. Dapper is removed.

## Consequences
- Type-safe queries, versioned schema, provider-agnostic code, and an easy path to pgvector (`Pgvector.EntityFrameworkCore`) for Phase 8.
- A larger change than A. Watch for N+1 queries and tracking overhead (`AsNoTracking`). The parity tests from Phase 0 must stay green against PostgreSQL (Testcontainers).
- Revisit the unfiltered-product-list quirk (returns 0 today) deliberately rather than by accident.

## Implementation notes (Phase 3a)
- `CatalogDbContext` (Npgsql + `EFCore.NamingConventions` snake_case). Tables `brands, types, tags, products, features`; ids are not generated (they come from the seed). `numeric(9,2)` price, `varchar(255)`, `text` description.
- **New integrity:** foreign keys products→brands/types (restrict), products→tags (nullable, set null), features→products (cascade). Indexes on every FK column plus a **unique index on `types.code`**. Idempotent DDL: [`catalog-schema.postgres.sql`](../catalog-schema.postgres.sql).
- Migration `InitialCatalog`. Applied by a **separate step**, `dotnet Tailwind.Traders.Web.dll --migrate` (compose `migrate` service; Kubernetes Job in Phase 4). `Database:MigrateOnStartup=true` is for local dev and tests only. The web app no longer touches the schema at startup.
- **Seeding:** CSVs vendored in `Standalone/Data/Seed` (no GitHub download). Typed CsvHelper class maps, a single transaction, insert-missing-by-Id (idempotent, and it repairs partial seeds).
- **Queries:** LINQ. `typeCodes.Contains(...)` → `= ANY(@p)`, `EF.Functions.ILike` with escaped `%`/`_`/`\`, type codes normalized to lower case (SQL Server CI collation parity), `AsNoTracking`, `AsSplitQuery` for detail. Dapper and Microsoft.Data.SqlClient are removed.
