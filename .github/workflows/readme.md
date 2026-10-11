# GitHub Actions workflows

| Workflow | Purpose |
|---|---|
| `pr_build_check.yaml` | PR: .NET tests (Testcontainers), frontend (Vitest + Vite build), Docker build, full compose stack + API checks + traces + Playwright smoke |
| `container.yaml` | Image build, Trivy scan (fails on CRITICAL), SBOM |
| `kubernetes.yaml` | Helm lint/template/kubeconform + install on kind |
| `terraform.yaml` | Terraform fmt/validate, tflint, checkov |
| `deploy-aws.yaml` | Deploy to EKS via GitHub OIDC (skips when AWS vars are unset) |
| `codeql-analysis.yml` | CodeQL |
| `tt-website.cicd.yml` | Legacy upstream pipeline (PR docker build only) |

Phase 7 deleted the legacy Azure workflows `container_appservice.yaml`, `regular_appservice.yaml` and `telephony_bot.yaml` (Azure App Service / ACS telephony bot, deprecated actions, never configured on this fork).
