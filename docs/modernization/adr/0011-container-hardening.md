# 0011. Container hardening: chiseled runtime, single-RID publish, scan gate

- **Status:** Accepted (Phase 2)
- **Date:** 2026-10-10

## Context
The legacy image built the SPA on `node:10-alpine` (Alpine 3.11, EOL) and ran on `aspnet:5.0` (Debian 10, EOL). It ran as root on port 80 with `ASPNETCORE_ENVIRONMENT=Development` in the build stage. Trivy reports **12 CRITICAL / 70 HIGH** in the runtime base alone. Phase 1 moved to `aspnet:10.0`, but the image was **956 MB**: OnnxRuntime and SkiaSharp NuGet packages carry native binaries for ~9 OS/CPU targets. The image is headed for Amazon EKS ([ADR 0009](0009-eks-and-helm.md)), where image size affects pull time and attack surface, and where AWS Graviton (arm64) is attractive on cost.

## Decision
- Multi-stage build: `node:22-alpine` (SPA) → `dotnet/sdk:10.0` → runtime **`dotnet/aspnet:10.0-noble-chiseled-extra`**. Chiseled Ubuntu is distroless: no shell, no package manager, non-root `app` user (uid 1654) by default. The `-extra` variant adds ICU and tzdata for globalization (SqlClient, cultures).
- `dotnet publish -r linux-x64|linux-arm64 --self-contained false`, chosen from `TARGETARCH`. Build stages run on `$BUILDPLATFORM`, so multi-arch builds (amd64 + arm64/Graviton) need no emulation for compilation.
- Port **8080** (`ASPNETCORE_HTTP_PORTS`), `ASPNETCORE_ENVIRONMENT=Production`, files owned by `app`, a tight `.dockerignore`.
- A cheap **`/healthz` liveness endpoint** (`AddHealthChecks` + `MapHealthChecks`). There is **no Dockerfile `HEALTHCHECK`**, because chiseled has no shell or curl; Kubernetes probes it (Phase 4). Readiness checks for the DB and DynamoDB come in Phase 6.
- Run read-only: compose uses `read_only`, `tmpfs /tmp`, `cap_drop: ALL` and `no-new-privileges`. The Helm chart will mirror this with `securityContext`.
- CI (`container.yaml`): build with BuildKit cache, smoke-test `/healthz` with a read-only filesystem, **Trivy scan with the build failing on CRITICAL**, and a CycloneDX **SBOM** artifact.
- Removed the Azure Dev Spaces `Dockerfile.develop`.

## Consequences
- Image **956 MB → 249 MB**, vulnerabilities (runtime) **195 → 3** (0 critical/high). See [container-scan.md](../container-scan.md).
- No shell means debugging uses `kubectl debug` with an ephemeral container, or the non-chiseled tag temporarily.
- Each image is tied to one CPU architecture. Multi-arch requires `docker buildx --platform linux/amd64,linux/arm64` (planned with ECR push in Phase 5).
- Base image CVEs still need patching via regular rebuilds (Dependabot/Renovate for base images is recommended).
