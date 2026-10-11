# 0009. Amazon EKS + Helm for hosting

- **Status:** Accepted. Implemented and verified on **kind** (Phase 4). **EKS rollout pending**: values-eks.yaml is ready, and the AWS infrastructure comes in Phase 5.
- **Date:** 2026-10-10

## Context
The upstream Helm chart uses chart apiVersion v1 and `networking.k8s.io/v1beta1` Ingress (removed in Kubernetes 1.22), plus Azure Dev Spaces values and AKS scripts. The goal is Kubernetes hosting on AWS.

## Decision
- **Amazon EKS** for cloud (EKS Auto Mode optional) and **kind** for local demos.
- Rewrite the **Helm** chart (apiVersion v2): Deployment with requests/limits, liveness/readiness/startup probes, non-root + read-only root filesystem, HPA, PDB, Service on 8080.
- Ingress via the **AWS Load Balancer Controller** (`ingressClassName: alb`, ACM certificate); No ingress on kind: a NodePort is mapped to localhost by kind `extraPortMappings` (ingress-nginx was retired in March 2026, so a local controller isn't worth demoing).
- Secrets from **AWS Secrets Manager** via **External Secrets Operator** (or Secrets Store CSI). Workload IAM via **EKS Pod Identity** (IRSA as fallback).
- EF Core migration bundle as a Helm pre-install/pre-upgrade hook Job.
- Optional IaC with Terraform (`terraform-aws-modules`) or AWS CDK.

## Consequences
- A standard, portable deployment unit; no secrets in values files.
- Cluster add-ons (ALB controller, External Secrets, ADOT, Pod Identity agent) must be provisioned first, ideally via IaC.
- Cost for EKS + Aurora in a demo account. Rehearse on kind and use Aurora Serverless v2.

## Implementation notes (Phase 4)
- The chart lives in [`deploy/helm/tailwindtraders`](../../../deploy/helm/tailwindtraders), and the legacy `Deploy/helm/web`, `DeployWebAKS.ps1` and `aks.yaml` workflow are deleted. See [kubernetes.md](../kubernetes.md).
- **Workload hardening:** UID 1654 (non-root), `readOnlyRootFilesystem`, all capabilities dropped, no privilege escalation, seccomp `RuntimeDefault`, `/tmp` as emptyDir, port 8080.
- **Probes:**
  - startup and liveness use `/healthz`, which checks only that the process is up, so a database outage never restarts pods.
  - readiness uses **`/readyz`**: PostgreSQL is reachable and seeded, and the DynamoDB table is ACTIVE.
- **Migrations:** a `--migrate` Job runs as a `pre-install,pre-upgrade` hook. The ConfigMap, Secret and ExternalSecret are hooks too (weight -10), so they exist before the Job runs. Trade-off: `helm uninstall` leaves them behind; delete the namespace to clean up.
- **Secrets:** `secrets.mode` = `secret` (local), `externalSecret` (ESO + Secrets Manager; the connection string is built from the RDS-managed secret JSON) or `existing`.
- **Identity:** a dedicated ServiceAccount. EKS Pod Identity needs no annotation, and there is an annotation slot for IRSA.
- **Scaling and availability:** HPA (autoscaling/v2, CPU 70 %), PDB (`maxUnavailable: 1`), zone topology spread, a memory limit and no CPU limit.
