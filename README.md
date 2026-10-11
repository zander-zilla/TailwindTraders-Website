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
| 2 | Hardened containers: chiseled .NET 10 image (956 → 249 MB, 0 critical/high CVEs), Trivy gate + SBOM in CI, local compose full stack | 🟡 In review | Phase 2 PR |
| 3a | SQL Server → PostgreSQL with EF Core 10 | ⚪ Planned | — |
| 3b | Cart MongoDB → Amazon DynamoDB | ⚪ Planned | — |
| 4 | Amazon EKS + Helm, Secrets Manager, ALB ingress, Pod Identity | ⚪ Planned | — |
| 5 | CI/CD to ECR/EKS via GitHub OIDC | ⚪ Planned | — |
| 6 | Health checks, OpenTelemetry → ADOT → CloudWatch/X-Ray | ⚪ Planned | — |
| 7–8 | Frontend upgrade; AI features (pgvector + Amazon Bedrock) (optional) | ⚪ Planned | — |

Details, estimates and risks: [PLAN.md](PLAN.md).

## AWS setup

> ⚠️ **The AWS infrastructure is planned and NOT yet implemented.** The code today still uses SQL Server and MongoDB. AWS resources, Helm charts, IaC and the deploy pipeline arrive in **Phases 3–6**. Nothing in this repo deploys to AWS yet.

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

The app currently runs in **Standalone mode**: it hosts its own product, cart and login APIs, backed by SQL Server + MongoDB.

**Option A: Docker Compose** (hardened web image + SQL Server 2022 + MongoDB 7; liveness at `/healthz`):
```bash
docker compose -f TailwindTraders.Website/Source/docker-compose.standalone.yml up --build
# open http://localhost:2000  (the DB is created and seeded on first start, which needs internet access for the seed CSVs)
```

**Option B: .NET 10 SDK + Node 22** (with SQL Server and MongoDB running somewhere):
```bash
cd TailwindTraders.Website/Source/Tailwind.Traders.Web
(cd ClientApp && npm ci --legacy-peer-deps && npm run build)
export SqlConnectionString='Server=localhost,1433;Database=tailwind;User Id=sa;Password=<pwd>;TrustServerCertificate=true'
export MongoConnectionString='mongodb://localhost:27017' ApiUrl=/api/v1 ApiUrlShoppingCart=/api/v1
dotnet run -p:BuildingDocker=true
```

**Tests** (Docker required; Testcontainers starts SQL Server and MongoDB):
```bash
cd TailwindTraders.Website/Source
dotnet test Tailwind.Traders.Web.Tests -p:BuildingDocker=true   # 12 tests
```
Container size and vulnerability comparison: [docs/modernization/container-scan.md](docs/modernization/container-scan.md). See the [test README](TailwindTraders.Website/Source/Tailwind.Traders.Web.Tests/README.md) for the pinned baseline values and the legacy bugs they uncovered.

## Original project

Tailwind Traders is a reference application created by **Microsoft**. All credit for the original app goes to the Tailwind Traders team. Related upstream repositories: [Tailwind Traders](https://github.com/Microsoft/TailwindTraders), [Backend](https://github.com/Microsoft/TailwindTraders-Backend), [Website](https://github.com/Microsoft/TailwindTraders-Website).

> **Legacy / unsupported in this fork:** the upstream Azure deployment instructions (Deploy to Azure button, App Service, AKS/Helm scripts in `TailwindTraders.Website/Deploy`, Azure Communication Services chat/video, Azure Personalizer) target .NET 5 and Azure. They are kept in the tree for reference only and are not maintained here. See the upstream README for them.

The original project is licensed under the MIT License (see [LICENSE](LICENSE)) and adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
