# Container image: before / after (Phase 2)

Scanned with **Trivy 0.75.0** (`--scanners vuln`, DB as of 2026-10-10) in the agent box. Sizes are as reported by `docker images` (uncompressed).

## Why the legacy image is scanned via its base images
The upstream image (`node:10-alpine` → `sdk:5.0` → `aspnet:5.0`) **could not be built in the agent box**. BuildKit fails on the `dotnet publish` layer with `failed to prepare … invalid argument`, which is an issue with the box's nested-Docker `vfs` storage driver and not with the Dockerfile. It was tried twice. So the "before" column combines the **legacy base images** with the **legacy NuGet advisories** reported by `dotnet restore` (NuGetAudit). The legacy app layer would only add to these numbers.

## Results

| Image | Base OS | Size | CRITICAL | HIGH | MEDIUM | LOW | Total |
|---|---|---|---|---|---|---|---|
| **Before:** `mcr.microsoft.com/dotnet/aspnet:5.0` (legacy runtime base) | Debian 10.12 (**EOL**) | 205 MB | **12** | **70** | 72 | 33 | **195** |
| **Before:** `node:10-alpine` (legacy SPA build stage) | Alpine 3.11 (**EOL**) | 83 MB | 9 | 56 | 18 | 2 | 85 |
| **Before:** legacy NuGet packages (ImageSharp beta, System.Data.SqlClient 4.8.2, MongoDB.Driver 2.11, JwtBearer 5.0.2, Azure.Storage.Blobs 12.8) | — | — | 0 | 5 | 9 | 0 | 14 |
| Phase 1 image (`aspnet:10.0`, all-RID publish) | Ubuntu 24.04 | 956 MB | 0 | 0 | 9 | 5 | 14 |
| **After (Phase 2):** `aspnet:10.0-noble-chiseled-extra`, single-RID publish | Ubuntu 24.04 chiseled | **249 MB** | **0** | **0** | 2 | 1 | **3** |

Notes:
- The build stage (`node:10-alpine`) does not ship in the final legacy image. It is listed because it builds the frontend and was on an EOL OS.
- The Phase 1 image was 956 MB because **OnnxRuntime and SkiaSharp ship native libraries for ~9 platforms** (win/osx/android/ios/linux, ~650 MB). Publishing for one runtime identifier (`linux-x64`, or `linux-arm64` for AWS Graviton) removes them. The chiseled base saves a further ~70 MB and removes the shell and package manager.
- The remaining 3 findings (2 medium, 1 low) are in Ubuntu base packages with no fixed version at scan time.
- Size numbers in the box use the `vfs` driver and are uncompressed. Registry (compressed) sizes are smaller. CI reports the size on a standard runner.

## Runtime hardening verified
- `docker run --read-only --tmpfs /tmp --cap-drop ALL --security-opt no-new-privileges` works: `/healthz` returns 200 "Healthy", the SPA serves, the products API works, the cart returns 401 when anonymous, and ONNX image search returns 200.
- Runs as **uid 1654 (`app`)**. `docker exec … sh` fails (**no shell** in the image).

## Reproduce
```bash
cd TailwindTraders.Website/Source/Tailwind.Traders.Web
docker build -t tt-web:phase2 .
trivy image --scanners vuln tt-web:phase2
trivy image --scanners vuln mcr.microsoft.com/dotnet/aspnet:5.0
```
CI: `.github/workflows/container.yaml` builds, smoke-tests, scans (fails on CRITICAL) and uploads a CycloneDX SBOM.
