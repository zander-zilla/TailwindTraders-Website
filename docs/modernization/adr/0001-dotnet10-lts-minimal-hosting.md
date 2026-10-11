# 0001. .NET 10 LTS with minimal hosting

- **Status:** Accepted (implemented in Phase 1, PR #2)
- **Date:** 2026-10-10

## Context
The app targeted `net5.0`, out of support since May 2022 with no security patches. It used the `Program` + `Startup` hosting model. .NET 8 LTS support ends in November 2026. .NET 10 is the current LTS release (November 2025, supported until November 2028).

## Decision
- Target **`net10.0`** for the web app and the tests.
- Merge `Startup.cs` into a **minimal hosting** `Program.cs` (`WebApplication.CreateBuilder`) and expose `public partial class Program` so tests can use `WebApplicationFactory<Program>`.
- Add `app.UseAuthorization()` (previously missing). Replace the Razor `Error` page with `AddProblemDetails()` + `UseExceptionHandler()`.
- Keep SQL Server + Dapper and MongoDB unchanged in this phase, so the upgrade is isolated from the data migration.

## Consequences
- Supported runtime until Nov 2028, current ASP.NET Core security fixes, smaller and faster startup code.
- Breaking changes handled: `System.Data.SqlClient` → `Microsoft.Data.SqlClient` (encrypted connections by default, so dev connection strings need `TrustServerCertificate=true`), CsvHelper culture argument, MongoDB.Driver 3.x, and HS256 JWT keys must be ≥ 256 bits (the demo key was changed, so old tokens are invalid).
- The Phase 0 characterization tests guard behaviour (11/11 passing).
