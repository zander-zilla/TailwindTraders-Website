# 0010. GitHub Actions OIDC to AWS IAM

- **Status:** Accepted (implementation planned for Phase 5)
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
