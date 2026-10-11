# SQL Server → Aurora PostgreSQL data migration (AWS DMS): runbook notes

> **Docs only.** The demo catalog is re-seeded from the CSVs bundled in the repo, so no data migration is needed for the demo itself. This runbook shows how a real customer database on SQL Server would move to the Phase 3a schema with **AWS DMS** (and **AWS SCT / DMS Schema Conversion** for assessment).

## Approach: the schema comes from EF Core, the data from DMS
1. **Target schema first.** Run the app's migrate step against Aurora PostgreSQL. This creates exactly the schema the code expects (snake_case names, FKs, indexes):
   ```bash
   docker run --rm -e ConnectionStrings__Products="Host=<aurora-writer>;Database=tailwind;Username=...;Password=...;SSL Mode=Require" \
     <ecr>/tailwindtraders/web:<tag> --migrate        # or apply docs/modernization/catalog-schema.postgres.sql
   ```
   Do **not** let DMS create the tables (target table prep mode: `DO_NOTHING`). Optionally run DMS Schema Conversion/SCT only as an assessment report.
2. **DMS endpoints:** a source SQL Server (CDC needs MS-CDC or MS-Replication enabled for ongoing replication) and a target Aurora PostgreSQL. Credentials go in **AWS Secrets Manager** and are referenced from the endpoints.
3. **Table mappings: rename to snake_case** (DMS transformation rules):

   | Source (SQL Server) | Target (PostgreSQL) |
   |---|---|
   | `dbo.Brands (Id, Name)` | `public.brands (id, name)` |
   | `dbo.Types (Id, Code, Name)` | `public.types (id, code, name)` |
   | `dbo.Tags (Id, Value)` | `public.tags (id, value)` |
   | `dbo.Products (Id, Name, Price, ImageName, BrandId, TypeId, TagId)` | `public.products (id, name, price, image_name, brand_id, type_id, tag_id)` |
   | `dbo.Features (Id, Title, Description, ProductItemId)` | `public.features (id, title, description, product_item_id)` |

   Use `convert-lowercase` on schema, table and column, plus `rename` rules for `ImageName` → `image_name` and so on (or one `rename` rule per column).
4. **FK ordering:** the new schema has FKs that the legacy one lacked. Either load parents first (brands, types, tags → products → features) or set `session_replication_role = replica` on the target during the full load (DMS extra connection attribute `afterConnectScript=SET session_replication_role='replica'`), then validate.
5. **Data quality pre-checks (legacy had no constraints):** orphaned `Products.BrandId/TypeId/TagId` and `Features.ProductItemId`, duplicate `Types.Code` (now unique), `TagId` values of 0 or blank (now `NULL`), `Name` NULLs (now NOT NULL), and `Price` scale > 2.
6. **Type mapping:** `INT` → `integer`, `NVARCHAR(255)` → `varchar(255)`, `NVARCHAR(MAX)` → `text`, `DECIMAL(9,2)` → `numeric(9,2)`. Collation: SQL Server CI comparisons are emulated in code (`ILIKE`, lower-cased codes). Consider `citext` only if other case-insensitive lookups appear.
7. **Run:** full load + CDC → enable **DMS data validation** → compare row counts and checksums → app cut-over (switch `ConnectionStrings__Products` in Secrets Manager / External Secrets) → stop the task.
8. **Rollback:** keep SQL Server read-only until validation passes. The app version is chosen by connection string only.

Effort for a real (small) catalog: about 0.5–1 day including validation.
