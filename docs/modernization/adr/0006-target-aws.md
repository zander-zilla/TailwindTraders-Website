# 0006. Target AWS as the cloud platform

- **Status:** Accepted
- **Date:** 2026-10-10

## Context
The legacy app is wired to Azure: App Service/AKS, Azure SQL, Cosmos DB, Blob Storage, Application Insights, Personalizer, Azure AD B2C, Azure Communication Services and Dev Spaces. The demo audience and owner standardize on AWS.

## Decision
Use AWS-native equivalents:

| Concern | Legacy (Azure) | Target (AWS) |
|---|---|---|
| Kubernetes | AKS | **Amazon EKS** (kind locally) |
| Registry | ACR / Docker Hub | **Amazon ECR** |
| Relational DB | Azure SQL | **Aurora / RDS PostgreSQL** |
| Cart store | Cosmos DB (Mongo API) | **Amazon DynamoDB** |
| Secrets | Key Vault / app settings | **AWS Secrets Manager** + External Secrets Operator |
| Ingress | AKS ingress (v1beta1) | **AWS Load Balancer Controller** (ALB + ACM) |
| Telemetry | Application Insights | **OpenTelemetry → ADOT → CloudWatch / X-Ray** |
| Blob | Azure Blob Storage | **Amazon S3** |
| CI auth | stored service-principal secrets | **GitHub OIDC → IAM role** |
| Identity | Azure AD B2C | Amazon Cognito, or keep the demo login |
| IaC | ARM templates | Terraform or AWS CDK (optional) |

## Consequences
- Azure-only features without a drop-in AWS equivalent (ACS video/chat, B2C) will be removed or feature-flagged for the demo.
- All infra and CI from the upstream repo (ARM, AKS workflows, Dev Spaces values) will be replaced.
- Application code stays cloud-portable where possible (OpenTelemetry, PostgreSQL); DynamoDB is the intentional AWS-specific choice.
