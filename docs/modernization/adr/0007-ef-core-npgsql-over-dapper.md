# 0007. EF Core 10 + Npgsql instead of Dapper

- **Status:** Proposed. The owner chose Option B; it moves to Accepted when Phase 3a is implemented.
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
