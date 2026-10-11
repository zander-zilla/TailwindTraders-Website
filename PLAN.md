# TailwindTraders-Website — AI-Assisted Modernization Plan

## Status / next step
- [x] Phase 0 — Baseline & safety net — DONE, merged (PRs #1/#3): 10 xUnit characterization tests (WebApplicationFactory + Testcontainers SQL Server 2022 + Mongo 7), all passing on .NET 5. Found 2 legacy bugs (no UseAuthorization → [Authorize] endpoints 500; login always 400) + unfiltered product list returns 0. Legacy Docker image build not reproduced in the agent box (docker vfs 'invalid argument' error in nested Docker) — see PR.
- [x] Phase 1 — .NET 5 → .NET 10 LTS — DONE, merged (PRs #2/#4): net10.0 + minimal hosting; Microsoft.Data.SqlClient; ImageSharp → **SkiaSharp** (ImageSharp 3.x CVEs, 4.x needs a commercial license); CsvHelper 33, Mongo 3.12, OnnxRuntime 1.22, Testcontainers 4.16; Personalizer, App Insights resource ids/subscription id, SpaServices, Razor Error page removed; 4 legacy bugs fixed; frontend builds on Node 22 (sass, react-scripts 5); Dockerfile .NET 10/Node 22 non-root :8080 builds and runs; 11/11 tests pass; 0 vulnerable NuGet packages in the web project. SQL Server + Dapper kept (Phase 3).
- [x] Phase 2 — Containerize properly — DONE, merged (PR #5): chiseled .NET 10 image 249 MB, 0 critical/high CVEs, Trivy gate + SBOM; compose runs on John's Mac.
- [x] Phase 3a — SQL Server → PostgreSQL 17 + EF Core 10 — DONE, merged (PR #6): CatalogDbContext (snake_case, FKs, FK indexes, unique types.code), migration InitialCatalog, `--migrate` one-shot step (compose `migrate` service → K8s Job later), bundled CSV seed (typed, transactional, idempotent, repairs partial), ILIKE search with escaped wildcards, Dapper/SqlClient removed, compose on postgres:17 (native arm64), 19/19 tests on Testcontainers PostgreSQL 17, ADR 0007 Accepted, DMS runbook (docs only).
- [x] Phase 3b — MongoDB cart → Amazon DynamoDB — DONE, merged (PR #7): table PK userId / SK productId, atomic ADD merge, TTL expiresAt (30 d), name/price/image looked up from the catalog, 404 on missing line, idempotent table creation in `--migrate` (`Cart:CreateTable=false` in AWS), endpoint/region/table config + SDK credential chain; MongoDB driver, browser Cosmos DAO and Cart AuthKey settings leak removed; compose uses amazon/dynamodb-local (native arm64); 23/23 tests on Testcontainers DynamoDB Local; ADR 0008 implemented.
- [x] Phase 4 — Kubernetes (Helm, kind; EKS values) — DONE for kind, merged (PR #8): new chart `deploy/helm/tailwindtraders` (hardened Deployment, startup/liveness `/healthz`, readiness `/readyz` = PostgreSQL + DynamoDB, HPA, PDB, SA for Pod Identity/IRSA, Service, Ingress with configurable class, migrate pre-install/pre-upgrade hook Job, ConfigMap, Secret/ExternalSecret toggle), values-kind / values-eks, `scripts/kind-up.sh`, CI kind install; legacy AKS chart/workflow removed. EKS rollout waits for Phase 5 infrastructure.
- [x] Phase 5 — AWS IaC + CI/CD — CODE DONE, merged (PR #9), NOT APPLIED (John deploys from his machine): `infra/terraform` (VPC 3 AZ/single NAT, EKS 1.37 Graviton t4g.large + Pod Identity Agent/metrics-server add-ons, LB Controller + ESO + ClusterSecretStore via Helm, ECR immutable+scan, Aurora PG 17 Serverless v2 0–4 ACU w/ managed secret, DynamoDB tt-cart TTL+PITR, Pod Identity roles, GitHub OIDC deploy role scoped to main/env `aws`, optional ACM/Route 53, outputs → GitHub vars/Helm values); `deploy-aws.yaml` (OIDC, arm64 build, ECR :sha, helm upgrade, ALB smoke; skips if unconfigured); `terraform.yaml` CI (fmt/validate/tflint/checkov); docs/modernization/aws-setup.md (bootstrap, ~$250–300/mo, teardown).
- [x] Phase 6 — Observability & resilience — DONE, merged (PR #10): OpenTelemetry .NET (ASP.NET Core, HttpClient, Npgsql, AWS SDK, runtime; OTLP via OTEL_* env), JSON logs w/ trace ids, custom cart/image-search spans + metrics, browser tracing (OTel web, traceparent → API), Aspire dashboard 13.6 in compose + kind (UI :18888), EKS → CloudWatch agent (amazon-cloudwatch-observability add-on in Terraform, Pod Identity CloudWatch + X-Ray) with 25 % sampling, standard HTTP resilience handler; TelemetryTests + CI asserts spans reach the dashboard; ADR 0012, observability.md.
- [x] Phase 7 — Frontend upgrade — DONE, PR #11 open (branch `phase-7-frontend`): CRA → Vite 7, React 18, react-router 7 (withRouter shim), react-redux 9, i18next 25, axios 1; removed ACS chat/video + Teams meeting, msal/B2C, App Insights, Fluent UI 7, react-s-alert, Bootstrap 4 CDN (it hid the login modal — legacy bug); /api/settings no longer serves ACS connection string/B2C/AppInsights; npm ci without --legacy-peer-deps; Vitest + Playwright smoke in CI (incl. browser↔API shared traces); deleted 3 legacy Azure workflows; ADR 0013.
- [ ] Phase 8 — AI feature showcase (optional) — NOT STARTED

**Cloud target: AWS** (EKS, ECR, RDS/Aurora PostgreSQL, Secrets Manager, ALB, ADOT/CloudWatch/X-Ray); local demo on kind. 

**Decisions recorded (2026-10-10), not yet implemented:**
- Data layer: **EF Core 10 + Npgsql (Option B) — CHOSEN**; keep-Dapper (Option A) NOT chosen. See §2a.
- Shopping cart: **MongoDB/Cosmos → Amazon DynamoDB** (AWSSDK.DynamoDBv2, PK = user/session id, TTL for abandoned carts, on-demand capacity, DynamoDB Local/LocalStack locally, IAM via EKS Pod Identity). jsonb/DocumentDB alternatives dropped.

**Next step:** review/merge Phase 7 PR #11; Phase 8 (pgvector semantic search + Amazon Bedrock) in progress. Repo: fork `zander-zilla/TailwindTraders-Website` (PRs target the fork only; never upstream).
Analysis clone (read-only): `/workspace/modernization/TailwindTraders-Website` (shallow, nothing pushed).

---

## 1. Current state (from repo inspection)

| Area | Finding |
|---|---|
| Solution | One project: `TailwindTraders.Website/Source/Tailwind.Traders.Web/Tailwind.Traders.Web.csproj` (ASP.NET Core + React SPA). Also a Bot Framework Composer bot (`TailwindTradersBotComposer/`) — out of scope. |
| Target framework | `net5.0` (EOL May 2022). Old `Program.cs` + `Startup.cs` hosting model. Docs still mention netcoreapp3.1. |
| Target LTS | **.NET 10** (LTS, Nov 2025, supported to Nov 2028). .NET 8 LTS ends Nov 2026 — so target .NET 10, not 8. |
| NuGet | Azure.Storage.Blobs 12.8.0; CsvHelper 12.1.2; Dapper 2.0.78; Microsoft.ApplicationInsights.AspNetCore 2.16.0; Microsoft.AspNetCore.Authentication.JwtBearer 5.0.2; Microsoft.AspNetCore.HeaderPropagation 5.0.2; Microsoft.AspNetCore.SpaServices.Extensions 5.0.2; Microsoft.Azure.CognitiveServices.Personalizer 1.0.0 (retired service); Microsoft.Extensions.Hosting 5.0.0; Microsoft.ML.OnnxRuntime 1.6.0; MongoDB.Driver 2.11.6; SixLabors.ImageSharp 1.0.0-beta0006 (pre-release, known CVEs); **System.Data.SqlClient 4.8.2** (deprecated). |
| Data access | **No EF Core.** Raw SQL via **Dapper** + `System.Data.SqlClient`, only in "Standalone" mode (`Standalone/` folder; enabled when `SqlConnectionString` is set). `StandaloneDatabaseInitializer` creates 5 tables (Brands, Types, Tags, Features, Products) with T-SQL DDL (`NVARCHAR(255)`, `NVARCHAR(MAX)`, `DECIMAL(9,2)`, `INT PRIMARY KEY` — no IDENTITY), then seeds them from CSVs downloaded at runtime from the TailwindTraders-Backend GitHub repo. `ProductService` uses simple SELECT/JOINs, Dapper list expansion `IN @TypeCodes`, and `LIKE @SearchKeyword`. No stored procedures, no IDENTITY, no T-SQL-only functions found. `SettingsController` regex-parses `Server=` out of the SQL connection string for the debug header. |
| Other data | Shopping cart in **MongoDB / Cosmos DB (Mongo API)** via MongoDB.Driver; frontend also has an `@azure/cosmos` cart DAO. |
| Backend dependency | Default ("frontend-only") mode calls TailwindTraders-Backend microservices (`ApiUrl` http://localhost:5200/v1, `ApiUrlShoppingCart` :3000). Standalone mode replaces them with in-process controllers (Products, Profile, Login, ShoppingCart, ImageClassifier). **Plan uses Standalone mode** to avoid the archived backend. |
| Frontend | CRA `react-scripts ^3.4.4`, **React 16.14**, react-router 4, redux 4 / react-redux 6, recompose, **node-sass 4** (won't build on modern Node), axios 0.18, msal 1.x (deprecated), Fluent UI 7 / office-ui-fabric-react, Azure Communication Services beta SDKs, App Insights JS. Docker build uses `node:10-alpine`. Served via `SpaServices.Extensions` (`UseSpa`, `UseReactDevelopmentServer`). |
| AI/ML already present | Embedded ONNX image-search model (`Standalone/OnnxModels/products.onnx`) or HTTP predictor; Azure Personalizer (retired); ACS chat/video; bot. |
| Containers | Multi-stage `Dockerfile` (node:10 → sdk:5.0 → aspnet:5.0), runs as root on port 80, `ASPNETCORE_ENVIRONMENT=Development` in build stage. `Dockerfile.develop`, `docker-compose.yml` (web only), `.devcontainer`. |
| K8s / Helm | Helm chart `Deploy/helm/web` (apiVersion v1 chart; Ingress `networking.k8s.io/v1beta1` — **removed in k8s 1.22**), values for AZDS (Dev Spaces, retired), B2C; `DeployWebAKS.ps1`; ARM `deployment.json`/azuredeploy. |
| CI/CD | GitHub Actions: aks.yaml (`ubuntu-16.04` runner — gone, `actions/checkout@v1`, Docker Hub), App Service workflows, CodeQL, PR build check. |
| Config/secrets | `appsettings.json` + env vars; connection strings as plain env/config; App Insights instrumentation key (deprecated in favour of connection string); hard-coded subscription id in csproj. |
| Health/tests | No health endpoints; only CRA's default `App.test.js`; no .NET test project. |

## 2a. Data-access modernization (Dapper vs EF Core) — DECISION: Option B (EF Core 10 + Npgsql)
**Today:** Dapper 2.0.78 micro-ORM + deprecated `System.Data.SqlClient`, with hand-written SQL Server SQL in `ProductService` (multi-map JOINs, `IN @list`, `LIKE`) and T-SQL `CREATE TABLE` strings in `StandaloneDatabaseInitializer`. No migrations, no schema versioning, runtime CSV seeding.

| | **A) Keep Dapper** (upgrade to 2.1.x + Npgsql) — ❌ NOT CHOSEN | **B) EF Core 10 + Npgsql provider** — ✅ CHOSEN |
|---|---|---|
| Changes | Swap driver to `NpgsqlDataSource`; hand-port SQL to PG dialect (`ILIKE`, `= ANY(@list)`, lowercase identifiers); add DbUp/Flyway for versioned SQL scripts | DbContext + entities, code-first migrations, LINQ replaces SQL strings, provider-agnostic queries |
| Effort | 1–2 days | 2–3.5 days |
| Pros | Smallest diff, lowest risk, max raw performance, minimal retesting | Type-safe queries, migrations/bundles for CI/CD & K8s Jobs, provider swap is mostly config, easy pgvector via `Pgvector.EntityFrameworkCore`, strong "AI rewrote my data layer" story |
| Cons | SQL stays hand-written & dialect-bound; schema still ad-hoc unless DbUp added | Larger change, learning curve, need to watch N+1 / tracking overhead |
| Demo value | Medium | **High** |

Dapper is removed entirely. The cart is not in PostgreSQL: it moves from MongoDB/Cosmos to **Amazon DynamoDB** (see Phase 3b).

## 2. Phases

### Phase 0 — Baseline & safety net (0.5–1 day)
1. Fork; branch per phase. Run Standalone mode with docker-compose (SQL Server 2022 + Mongo) on .NET 5 SDK container to record "before" screenshots/timings.
2. Add an xUnit integration-test project (WebApplicationFactory + Testcontainers) covering `/api/v1/products`, product detail, filters, cart add/remove. Add a Playwright smoke test for the home/list/detail pages.
3. Capture these as golden responses (JSON snapshots) so later phases prove behaviour parity.
- **AI talking points:** AI inventories the repo and writes this plan; AI generates characterization tests from controllers and builds golden snapshots — "tests before touching anything."

### Phase 1 — Upgrade .NET 5 → .NET 10 LTS (1–2 days)
1. `TargetFramework` → `net10.0`; add `global.json`; enable nullable/implicit usings (optional, warnings only).
2. Merge `Startup.cs` into minimal-hosting `Program.cs` (`WebApplication.CreateBuilder`).
3. Package updates: remove `Microsoft.Extensions.Hosting` (in framework); JwtBearer/HeaderPropagation → 10.x; `System.Data.SqlClient` → `Microsoft.Data.SqlClient` (interim, before Phase 3); CsvHelper 33.x (API changes: `CsvConfiguration`, `GetRecords`); (MongoDB.Driver not upgraded — replaced by DynamoDB in Phase 3b); ImageSharp 3.x (API changes, licence check — Six Labors Split License); OnnxRuntime 1.2x; Azure.Storage.Blobs → replaced by AWSSDK.S3; App Insights SDK → removed in favour of OpenTelemetry (Phase 6, ADOT).
4. Remove/feature-flag Azure Personalizer (retired) — replace with simple "recommended" logic or Phase 8 AI. Azure Blob image upload → Amazon S3 (`AWSSDK.S3`).
5. SPA hosting: replace `SpaServices.Extensions`/`UseReactDevelopmentServer` with static files + `MapFallbackToFile("index.html")` (and Vite proxy in dev once Phase 7 is done).
6. Run .NET Upgrade Assistant / GitHub Copilot app modernization for .NET; fix analyzers; tests green.
- **AI talking points:** Copilot "upgrade to .NET 10" agent produces the plan + PR; AI explains each breaking change (CsvHelper, ImageSharp); AI converts Startup → minimal hosting.

### Phase 2 — Containerize properly (0.5–1 day)
1. New multi-stage Dockerfile: `node:22-alpine` (or 24 LTS) for SPA, `mcr.microsoft.com/dotnet/sdk:10.0` build, `aspnet:10.0` (or `-noble-chiseled`) runtime; non-root user (`APP_UID`), port 8080, `ASPNETCORE_ENVIRONMENT=Production`.
2. Alternatively demo `dotnet publish /t:PublishContainer` (SDK containers) for the backend.
3. docker-compose for local dev: web + postgres + DynamoDB Local or LocalStack (+ otel collector/Aspire dashboard). Remove `Dockerfile.develop`/AZDS leftovers.
4. Image scanning (Trivy) — show CVE count before vs after.
- **AI talking points:** AI writes the hardened Dockerfile and explains chiseled images; CVE before/after slide is a strong customer visual.

### Phase 3a — SQL Server → PostgreSQL with EF Core 10 + Npgsql (2–3.5 days)
Decision recorded in §2a: **Option B (EF Core 10 + Npgsql)**; Dapper and `System.Data.SqlClient` are removed.
1. **Model (0.5 day):** scaffold `TailwindDbContext` + entities (Brand, Type, Tag, Feature, Product) from the existing C# models/DDL; Fluent API config (snake_case via `EFCore.NamingConventions`, `numeric(9,2)`, `text`), relationships Product→Brand/Type, Product→Features.
2. **Migrations (0.5 day):** `dotnet ef migrations add Initial`; replace the hand-written T-SQL DDL in `StandaloneDatabaseInitializer`; emit an idempotent script / migration bundle (`dotnet ef migrations bundle`) run as a Kubernetes Job / Helm pre-upgrade hook (not on app startup).
3. **Seeding (0.5 day):** vendor CSVs into the repo (stop runtime downloads from TailwindTraders-Backend); seed via EF `HasData`/`UseAsyncSeeding` (EF 9+) or Npgsql binary `COPY`.
4. **Queries (0.5–1 day):** rewrite `ProductService` Dapper SQL as LINQ (`Include`, `Where(t => typeCodes.Contains(...))`, `EF.Functions.ILike` to keep SQL Server case-insensitive search), `AsNoTracking`, pagination. Register with `AddDbContextPool` + `UseNpgsql`; connection string `ConnectionStrings:Products` (keep `SqlConnectionString` fallback one release).
5. **Data migration story (0.5 day):** SQL Server → PostgreSQL via **AWS DMS** (+ SCT for schema assessment) into **Amazon RDS / Aurora PostgreSQL**; pgloader for local. Locally/kind: Postgres container or CloudNativePG.
6. **Debug header:** update `SettingsController` to parse `Host=` → live "SQL Server → Aurora PostgreSQL" visual.
7. **Parity (0.5 day):** run Phase 0 golden tests against PostgreSQL via Testcontainers.
- **AI talking points:** AI translates T-SQL DDL + hand-written SQL into an EF Core model and LINQ, flags dialect gotchas (case-sensitive LIKE, identifier folding), generates migrations and parity tests; show generated SQL from `ToQueryString()`.

### Phase 3b — Shopping cart: MongoDB/Cosmos → Amazon DynamoDB (1.5–2 days)
1. **Table design (0.25 day):** table `tt-cart`, partition key `cartId` (= user id, or anonymous session id), sort key `productId`; attributes qty, price, name, imageUrl, `updatedAt`, `expiresAt` (epoch seconds) with **TTL** enabled for abandoned carts (e.g. 30 days, refreshed on each write); **on-demand (PAY_PER_REQUEST)** capacity.
2. **Code (0.5–1 day):** replace `MongoDB.Driver` + `ShoppingCartItemDocument` in `Standalone/Controllers/ShoppingCartController.cs` and `ConfigExtensions.cs` with an `ICartRepository` backed by **AWSSDK.DynamoDBv2** (low-level `IAmazonDynamoDB` or `DynamoDBContext` object persistence) registered via `AWSSDK.Extensions.NETCore.Setup`; `Query` by `cartId`, conditional `UpdateItem` for quantity. Remove `MongoConnectionString`, Mongo debug-header parsing, and the frontend `@azure/cosmos` cart DAO.
3. **Local/kind (0.25 day):** DynamoDB Local or LocalStack container in docker-compose/kind; `ServiceURL` override via config; table bootstrap script for local only.
4. **AWS (0.25 day):** table created via Terraform/CDK (or CLI in the demo); IAM role with least-privilege table policy bound through **EKS Pod Identity**; no access keys anywhere.
5. **Tests (0.25–0.5 day):** repository integration tests with Testcontainers (DynamoDB Local); cart flows in Phase 0 golden/Playwright tests stay green.
- **AI talking points:** AI designs the single-table key schema from the existing Mongo document, writes the repository + IAM policy, and generates local bootstrap and tests.

### Phase 4 — Kubernetes on Amazon EKS with Helm (1.5–2.5 days)
1. Rewrite chart (apiVersion v2): Deployment with requests/limits, readiness/liveness/startup probes, securityContext (non-root, readOnlyRootFilesystem), HPA, PDB, Service on 8080.
2. Ingress via **AWS Load Balancer Controller** (`ingressClassName: alb`, ACM certificate annotation) — local kind uses ingress-nginx.
3. Secrets: **AWS Secrets Manager** → K8s Secrets via **External Secrets Operator** (or Secrets Store CSI Driver + AWS provider), authenticated with **EKS Pod Identity** (or IRSA). No passwords in values files; RDS can use IAM auth / Secrets Manager rotation.
4. Data services: **Amazon RDS / Aurora PostgreSQL** (pgvector supported). Cart: **Amazon DynamoDB** table (on-demand, TTL enabled) — created by IaC/Helm-adjacent setup; pod accesses it via **EKS Pod Identity** IAM role scoped to that table (`dynamodb:GetItem/PutItem/UpdateItem/DeleteItem/Query`). Local kind: DynamoDB Local or LocalStack.
5. EF Core migration bundle as a Helm pre-install/upgrade hook Job.
6. Images in **Amazon ECR**; product images optionally S3 + CloudFront.
7. Targets: local `kind`/k3d for the demo; EKS (optionally EKS Auto Mode) for cloud. Delete AKS/AZDS values, `DeployWebAKS.ps1`, ARM templates, v1beta1 ingress.
8. **IaC (optional, +1–2 days):** Terraform (`terraform-aws-modules/eks`, `rds-aurora`, ECR, IAM OIDC role) or AWS CDK (C#/TypeScript) for VPC, EKS, Aurora, ECR, Secrets Manager, ALB controller.
- **AI talking points:** AI generates chart, values and Terraform/CDK, explains every probe/securityContext/IAM policy; AI lints with `helm lint`/kube-score/tfsec and fixes findings.

### Phase 5 — CI/CD with GitHub Actions → AWS (0.5–1 day)
1. Replace old workflows: build+test (.NET 10, Node 22) → Docker build with cache → Trivy scan → push to **ECR** → `helm upgrade --install` to **EKS**.
2. Auth via **GitHub OIDC → AWS IAM role** (`aws-actions/configure-aws-credentials`), no stored AWS keys; `aws-actions/amazon-ecr-login`.
3. PR checks: tests (Testcontainers PG), Playwright smoke, CodeQL (keep), Dependabot; environments with approvals; optional `terraform plan` on PR.
- **AI talking points:** AI writes workflows and least-privilege IAM trust policy; Copilot code review; AI fixes a failing pipeline from logs.

### Phase 6 — Observability, health & resilience (0.5–1 day)
1. `AddHealthChecks()` with EF Core/Npgsql + a DynamoDB check (`DescribeTable`); `/healthz/live`, `/healthz/ready`.
2. OpenTelemetry SDK (traces/metrics/logs, EF Core + Npgsql + HttpClient + AWS SDK/DynamoDB instrumentation via `OpenTelemetry.Instrumentation.AWS`) → OTLP → **AWS Distro for OpenTelemetry (ADOT)** collector (EKS add-on) → **CloudWatch** (metrics/logs, Container Insights, Application Signals) and **X-Ray** traces. Replaces Application Insights (server SDK + JS SDK; frontend → CloudWatch RUM optional). **.NET Aspire** dashboard for local dev.
3. `Microsoft.Extensions.Http.Resilience` on outbound HttpClients.
- **AI talking points:** AI adds instrumentation; show a trace browser → API → Aurora PostgreSQL in X-Ray/CloudWatch.

### Phase 7 — Frontend upgrade (stretch, 2–4 days)
1. CRA 3 → **Vite**; React 16 → 18/19; react-router 4 → 6/7; `node-sass` → `sass` (Dart Sass); recompose → hooks; react-redux 9 / Redux Toolkit; axios 1.x; msal 1 / Azure AD B2C → Amazon Cognito (`aws-amplify` auth or `oidc-client-ts`) or keep the demo login; Fluent UI 7 → 9 (largest effort — could keep 8).
2. Remove unused/beta ACS packages or update to GA.
- **AI talking points:** AI-driven codemods (class components/HOCs → hooks) across ~50 files is visually impressive; keep scope to a few pages if time-boxed.

### Phase 8 — AI feature showcase (optional, 1–2 days)
- Replace retired Azure Personalizer with **semantic product search / recommendations using pgvector** on Aurora/RDS PostgreSQL (`Pgvector.EntityFrameworkCore`), embeddings from **Amazon Bedrock** (e.g. Titan/Cohere embeddings).
- Shopping assistant chat (Microsoft.Extensions.AI / Semantic Kernel with Bedrock) grounded on the catalog, replacing the legacy Azure Bot/ACS chat.

**Total core (Phases 0–6): ~7.5–13 dev days (EF Core 2–3.5 + DynamoDB cart 1.5–2); +1–2 for IaC; with stretch 10.5–19.**

## 3. Top extra ideas (ranked for demo value)
1. **pgvector semantic search / recommendations** on Aurora PostgreSQL + Bedrock embeddings (replaces retired Personalizer; showcases why PostgreSQL + EF Core).
2. **OpenTelemetry → ADOT → CloudWatch/X-Ray** (+ .NET Aspire locally): end-to-end trace visual.
3. **Hardened chiseled containers + Trivy CVE before/after**, plus zero-secrets (GitHub OIDC → IAM, EKS Pod Identity, Secrets Manager).
4. Characterization tests + Testcontainers parity proof (credibility for the DB/ORM swap).
5. Show DynamoDB TTL expiring abandoned carts + CloudWatch metrics for the cart table (serverless, zero-ops data store story).
6. IaC with Terraform or CDK — whole AWS environment reproducible.
7. Frontend Vite/React 18 codemod (stretch).

## 4. Risks / blockers
- **Backend dependency:** default mode needs TailwindTraders-Backend (archived, also old .NET). Mitigation: demo Standalone mode only; document frontend-only mode as out of scope.
- **Runtime seed download** from the Backend repo's raw GitHub URLs — breaks if moved/removed; vendor the CSVs.
- **Azure-coupled features on an AWS target:** Personalizer (retired), Azure Dev Spaces, Azure Communication Services chat/video (beta SDKs), Azure AD B2C + msal 1.x login, Azure Blob uploads, App Insights, Cosmos cart DAO in frontend. Need AWS equivalents (S3, Cognito or keep demo login, ADOT) or removal — ACS video/chat has no drop-in AWS replacement (Chime SDK is a rewrite); recommend removing/feature-flagging for the demo.
- **EF Core migration scope creep:** keep LINQ rewrite limited to `ProductService`; watch N+1 / tracking (`AsNoTracking`, compiled queries); Dapper is not a fallback (decision recorded).
- **AWS cost/quotas** for EKS + Aurora in a demo account; use kind + Postgres container for rehearsals, Aurora Serverless v2 for cloud.
- **Frontend build is broken on modern Node** (node-sass 4, CRA 3 + Node 10). Even Phase 1–2 require at least a minimal frontend build fix (swap node-sass → sass, possibly keep Node 16/18 temporarily) or Vite migration.
- ~~ImageSharp licence/API breaks~~ — resolved in Phase 1 by switching to SkiaSharp (MIT); ONNX image search covered by a smoke test.
- ~~**Frontend still legacy**~~ (resolved in Phase 7) after Phase 1 (React 16, CRA 5 needs `--legacy-peer-deps`, ACS/Fluent/Cosmos pinned to 2021 versions, large bundle) — Phase 7.
- JWT demo signing key changed (>=256 bits required by .NET 10); tokens issued by the old app are invalid — set `SecurityKey` via secrets in Phase 4.
- **CsvHelper** breaking API changes.
- **PG dialect:** case-sensitive LIKE, identifier case folding, seeding throughput; no stored procs/IDENTITY, so risk is low overall.
- Hard-coded Azure subscription id in csproj; no existing tests → regressions hard to detect until Phase 0 is done.
- **DynamoDB cart:** frontend `@azure/cosmos` cart DAO and `ByPassShoppingCartApi` path must be removed so all cart access goes through the API; existing Mongo cart data is not migrated (carts are ephemeral — acceptable for demo); item-size limit 400 KB per cart item (fine with one item per product); DynamoDB Local/LocalStack behaviour differences (TTL not enforced locally — demo TTL in AWS only); IAM misconfiguration of Pod Identity is a common first-deploy failure.
