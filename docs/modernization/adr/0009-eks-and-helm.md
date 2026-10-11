# 0009. Amazon EKS + Helm for hosting

- **Status:** Accepted (implementation planned for Phase 4)
- **Date:** 2026-10-10

## Context
The upstream Helm chart uses chart apiVersion v1 and `networking.k8s.io/v1beta1` Ingress (removed in Kubernetes 1.22), plus Azure Dev Spaces values and AKS scripts. The goal is Kubernetes hosting on AWS.

## Decision
- **Amazon EKS** for cloud (EKS Auto Mode optional) and **kind** for local demos.
- Rewrite the **Helm** chart (apiVersion v2): Deployment with requests/limits, liveness/readiness/startup probes, non-root + read-only root filesystem, HPA, PDB, Service on 8080.
- Ingress via the **AWS Load Balancer Controller** (`ingressClassName: alb`, ACM certificate); ingress-nginx on kind.
- Secrets from **AWS Secrets Manager** via **External Secrets Operator** (or Secrets Store CSI). Workload IAM via **EKS Pod Identity** (IRSA as fallback).
- EF Core migration bundle as a Helm pre-install/pre-upgrade hook Job.
- Optional IaC with Terraform (`terraform-aws-modules`) or AWS CDK.

## Consequences
- A standard, portable deployment unit; no secrets in values files.
- Cluster add-ons (ALB controller, External Secrets, ADOT, Pod Identity agent) must be provisioned first, ideally via IaC.
- Cost for EKS + Aurora in a demo account. Rehearse on kind and use Aurora Serverless v2.
