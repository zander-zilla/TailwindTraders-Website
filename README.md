# Tailwind Traders Website: AI-assisted modernization demo

> **What is this?** A fork of [microsoft/TailwindTraders-Website](https://github.com/microsoft/TailwindTraders-Website), modernized step by step as a **demo of AI-assisted application modernization**. Each phase lands as a reviewed pull request, with its design decisions recorded as ADRs.

![Tailwind Traders Website](Documents/Images/Website.png)

## From → to

| | Legacy (upstream) | Target (this fork) |
|---|---|---|
| Runtime | .NET 5 (EOL), `Startup.cs`, SpaServices | **.NET 10 LTS**, minimal hosting, SPA served as static files |
| Hosting | Azure App Service / AKS | **Amazon EKS** with Helm (kind locally) |
| Catalog DB | SQL Server + Dapper + hand-written T-SQL | **Aurora PostgreSQL + EF Core 10** (Npgsql) |
| Shopping cart | Cosmos DB / MongoDB | **Amazon DynamoDB** |
| Images / registry | Docker Hub / ACR | **Amazon ECR** |
| Secrets | App settings / Key Vault | **AWS Secrets Manager** (External Secrets, EKS Pod Identity) |
| Telemetry | Application Insights | **OpenTelemetry → ADOT → CloudWatch / X-Ray** |
| CI/CD | Old GitHub Actions (ubuntu-16.04, stored secrets) | GitHub Actions with **OIDC → AWS IAM role** |

See the architecture diagrams in **[docs/modernization](docs/modernization/README.md)**.

## Status

| Phase | Scope | Status | PR |
|---|---|---|---|
| 0 | Baseline characterization tests (xUnit + WebApplicationFactory + Testcontainers) | ✅ Done | [#1](https://github.com/zander-zilla/TailwindTraders-Website/pull/1) |
| 1 | .NET 5 → .NET 10, package upgrades, legacy bug fixes (incl. a connection-string leak), Node 22 frontend build | ✅ Done | [#2](https://github.com/zander-zilla/TailwindTraders-Website/pull/2) |
| 2 | Hardened containers: chiseled .NET 10 image (956 → 249 MB, 0 critical/high CVEs), Trivy gate + SBOM in CI, local compose full stack | ✅ Done | [#5](https://github.com/zander-zilla/TailwindTraders-Website/pull/5) |
| 3a | SQL Server → PostgreSQL 17 with EF Core 10: migrations + `--migrate` step, bundled idempotent seed, case-insensitive search | ✅ Done | [#6](https://github.com/zander-zilla/TailwindTraders-Website/pull/6) |
| 3b | Cart MongoDB → Amazon DynamoDB: PK user / SK product, TTL, server-side prices, DynamoDB Local | ✅ Done | [#7](https://github.com/zander-zilla/TailwindTraders-Website/pull/7) |
| 4 | Kubernetes: new Helm chart (hardened, probes, HPA/PDB, migrate hook Job), kind locally, EKS values (ALB, Secrets Manager via ESO, Pod Identity) | ✅ Done | [#8](https://github.com/zander-zilla/TailwindTraders-Website/pull/8) |
| 5 | AWS infrastructure as Terraform + `deploy-aws` GitHub Actions (OIDC → ECR → EKS); validated, **not yet applied** | ✅ Done (not yet applied) | [#9](https://github.com/zander-zilla/TailwindTraders-Website/pull/9) |
| 6 | Observability: OpenTelemetry end to end (browser → API → PostgreSQL/DynamoDB), Aspire dashboard locally, CloudWatch/X-Ray on EKS, HTTP resilience | ✅ Done | [#10](https://github.com/zander-zilla/TailwindTraders-Website/pull/10) |
| 7 | Frontend: Vite 7, React 18, react-router 7; Azure-only UI (ACS video/chat, B2C, App Insights) removed; Vitest + Playwright | 🟡 In review | [#11](https://github.com/zander-zilla/TailwindTraders-Website/pull/11) |
| 8 | AI: semantic product search (pgvector + Amazon Bedrock Titan embeddings; offline provider locally), optional RAG assistant | 🟡 In review | Phase 8 PR |

Details, estimates and risks: [PLAN.md](PLAN.md).

## AWS setup

> ⚠️ **The AWS infrastructure is written but NOT yet deployed.** Phase 5 adds Terraform for everything (`infra/terraform`) and a `deploy-aws` workflow. Both are statically validated, but no AWS account has been connected yet, so nothing runs in AWS. Bootstrap steps, cost and teardown are in **[docs/modernization/aws-setup.md](docs/modernization/aws-setup.md)**. Locally the app runs on compose or kind ([kubernetes.md](docs/modernization/kubernetes.md)).

What the target deployment will require:
- An **AWS account** and region, with permissions to create the resources below (Terraform or AWS CDK is planned as optional IaC).
- **Amazon EKS** cluster with the AWS Load Balancer Controller, External Secrets Operator, the ADOT add-on and the EKS Pod Identity agent ([ADR 0009](docs/modernization/adr/0009-eks-and-helm.md)).
- **Amazon Aurora PostgreSQL** (or RDS PostgreSQL) for the catalog ([ADR 0006](docs/modernization/adr/0006-target-aws.md), [data flow](docs/modernization/data-flow.md)).
- **Amazon DynamoDB** table for the cart: partition key `cartId`, TTL, on-demand capacity ([ADR 0008](docs/modernization/adr/0008-dynamodb-for-cart.md)).
- **Amazon ECR** repository for the web image.
- **AWS Secrets Manager** entries for the database credentials and the JWT signing key.
- An **IAM OIDC identity provider + role** for GitHub Actions; no long-lived AWS keys ([ADR 0010](docs/modernization/adr/0010-github-oidc-to-aws.md)).

Further reading: [modernization overview](docs/modernization/README.md) · [current-state data flow](docs/modernization/data-flow.md) · [all ADRs](docs/modernization/adr/) · [PLAN.md](PLAN.md)

## Run it locally today

The app currently runs in **Standalone mode**: it hosts its own product, cart and login APIs, backed by PostgreSQL 17 (catalog, EF Core) + Amazon DynamoDB (cart; DynamoDB Local for development).

**Option A: Docker Compose** (hardened web image + PostgreSQL 17 + DynamoDB Local, all native on Apple Silicon; a one-shot `migrate` service applies EF Core migrations + seed and creates the cart table; liveness at `/healthz`):
```bash
docker compose -f TailwindTraders.Website/Source/docker-compose.standalone.yml up --build
# open http://localhost:2000  (seed data is bundled in the repo; no internet needed)
```

**Option B: .NET 10 SDK + Node 22** (with PostgreSQL and DynamoDB Local running somewhere, e.g. `docker run -p 8000:8000 amazon/dynamodb-local`):
```bash
cd TailwindTraders.Website/Source/Tailwind.Traders.Web
(cd ClientApp && npm ci && npm run build)   # Vite; `npm run dev` = hot-reload on :3000 proxying /api to :2000; `npm test` = Vitest; `npm run e2e` = Playwright
export ConnectionStrings__Products='Host=localhost;Database=tailwind;Username=tailwind;Password=<pwd>'
export Cart__ServiceUrl='http://localhost:8000' ApiUrl=/api/v1 ApiUrlShoppingCart=/api/v1   # omit ServiceUrl in AWS (credential chain / Pod Identity)
dotnet run -p:BuildingDocker=true -- --migrate   # apply migrations + seed, then exit
dotnet run -p:BuildingDocker=true
```

**Traces:** both compose and kind include the .NET Aspire dashboard at http://localhost:18888 ([observability.md](docs/modernization/observability.md)).

**Option C: Kubernetes on kind** (Docker Desktop + `brew install kind helm kubectl`): `scripts/kind-up.sh` creates the cluster, builds and loads the image, installs the Helm chart (the migrate hook Job runs first) and serves http://localhost:2000. Use `scripts/kind-up.sh --down` to remove it. See [kubernetes.md](docs/modernization/kubernetes.md).

**Tests** (Docker required; Testcontainers starts PostgreSQL 17 and DynamoDB Local):
```bash
cd TailwindTraders.Website/Source
dotnet test Tailwind.Traders.Web.Tests -p:BuildingDocker=true   # 23 tests
```
Container size and vulnerability comparison: [docs/modernization/container-scan.md](docs/modernization/container-scan.md). See the [test README](TailwindTraders.Website/Source/Tailwind.Traders.Web.Tests/README.md) for the pinned baseline values and the legacy bugs they uncovered.

## Original project

Tailwind Traders is a reference application created by **Microsoft**. All credit for the original app goes to the Tailwind Traders team. Related upstream repositories: [Tailwind Traders](https://github.com/Microsoft/TailwindTraders), [Backend](https://github.com/Microsoft/TailwindTraders-Backend), [Website](https://github.com/Microsoft/TailwindTraders-Website).

> **Legacy / unsupported in this fork:** the upstream Azure deployment instructions (Deploy to Azure button, App Service, AKS/Helm scripts in `TailwindTraders.Website/Deploy`, Azure Communication Services chat/video, Azure Personalizer) target .NET 5 and Azure. They are kept in the tree for reference only and are not maintained here. See the upstream README for them.

The original project is licensed under the MIT License (see [LICENSE](LICENSE)) and adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
