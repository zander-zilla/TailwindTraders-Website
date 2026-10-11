# 0010. GitHub Actions OIDC to AWS IAM

- **Status:** Accepted. Implemented as code in Phase 5 (Terraform + `deploy-aws.yaml`); **not yet applied** (no AWS account yet).
- **Date:** 2026-10-10

## Context
Upstream workflows store long-lived credentials as repo secrets (Docker Hub password, Azure service-principal JSON) and use retired runners (`ubuntu-16.04`).

## Decision
- Authenticate GitHub Actions to AWS with **OIDC federation**: an IAM role whose trust policy is restricted to this repo and to specific branches/environments, via `aws-actions/configure-aws-credentials`.
- Push images to **ECR** (`aws-actions/amazon-ecr-login`) and deploy with `helm upgrade --install` to EKS. The role has least-privilege ECR push and EKS access entry permissions.
- PR checks: tests with Testcontainers plus a Docker build (already in `pr_build_check.yaml` since Phase 1), Trivy scan, CodeQL, Dependabot. Protected environments with approvals for prod.

## Consequences
- No long-lived AWS keys in GitHub; credentials are short-lived and auditable in CloudTrail.
- Requires a one-time IAM OIDC provider and role setup (IaC recommended).
- GitHub Actions must be enabled on the fork; it currently appears disabled.

## Implementation notes (Phase 5)
- `infra/terraform/identity.tf`: OIDC provider `token.actions.githubusercontent.com` (aud `sts.amazonaws.com`; can reuse an existing one), role `<prefix>-github-deploy`. Trust `sub` is **exactly** `repo:zander-zilla/TailwindTraders-Website:ref:refs/heads/main` or `…:environment:aws`, so PRs, forks and other branches can't assume it.
- **Least privilege:** ECR push to the single repository, `eks:DescribeCluster`. Kubernetes access comes from an **EKS access entry** scoped to `AmazonEKSAdminPolicy` in the `tailwindtraders` namespace only (not cluster-admin).
- `.github/workflows/deploy-aws.yaml`: `id-token: write`, `aws-actions/configure-aws-credentials` with `vars.AWS_ROLE_ARN`. Configuration lives in **repository variables** (all non-secret ARNs and endpoints from `terraform output github_variables`); there are no AWS keys in GitHub. Native arm64 build on `ubuntu-24.04-arm`, push `:<sha>` (immutable tags), `helm upgrade --install --wait` (migrate hook), then a smoke test through the ALB. When `AWS_ROLE_ARN` is unset it skips with a notice.
- Optional hardening: required reviewers on the `aws` environment.
