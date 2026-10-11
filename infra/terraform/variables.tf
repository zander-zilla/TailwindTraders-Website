variable "account_id" {
  description = "AWS account id to deploy into (provider refuses any other account)."
  type        = string
  validation {
    condition     = can(regex("^[0-9]{12}$", var.account_id))
    error_message = "account_id must be a 12-digit AWS account id."
  }
}

variable "region" {
  description = "AWS region, e.g. us-east-1, us-west-2, ca-central-1."
  type        = string
  default     = "us-east-1"
}

variable "name_prefix" {
  description = "Prefix for resource names (lowercase, short)."
  type        = string
  default     = "tt-demo"
  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{1,15}$", var.name_prefix))
    error_message = "name_prefix: 2-16 chars, lowercase letters, digits, dashes."
  }
}

variable "tags" {
  description = "Extra tags for all resources."
  type        = map(string)
  default     = {}
}

# ---------- network ----------
variable "vpc_cidr" {
  type    = string
  default = "10.42.0.0/16"
}

# ---------- EKS ----------
variable "kubernetes_version" {
  description = "EKS Kubernetes version (1.37 = newest on EKS as of Oct 2026)."
  type        = string
  default     = "1.37"
}

variable "node_instance_types" {
  description = "Graviton (arm64) instance types for the managed node group."
  type        = list(string)
  default     = ["t4g.large"]
}

variable "node_min_size" {
  type    = number
  default = 2
}

variable "node_max_size" {
  type    = number
  default = 4
}

variable "node_desired_size" {
  type    = number
  default = 2
}

variable "cluster_endpoint_public_access_cidrs" {
  description = "CIDRs allowed to reach the public EKS API endpoint (GitHub-hosted runners need it public; restrict to your IPs if you use self-hosted runners)."
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

variable "admin_principal_arns" {
  description = "Extra IAM principals (users/roles, e.g. your SSO admin role) granted cluster-admin via EKS access entries."
  type        = list(string)
  default     = []
}

# ---------- app ----------
variable "app_namespace" {
  type    = string
  default = "tailwindtraders"
}

variable "app_service_account" {
  type    = string
  default = "tailwindtraders-web"
}

variable "cart_table_name" {
  type    = string
  default = "tt-cart"
}

variable "database_name" {
  type    = string
  default = "tailwind"
}

variable "aurora_min_acu" {
  description = "Aurora Serverless v2 minimum ACUs (0 = auto-pause when idle, supported on PostgreSQL 13.15+/14.12+/15.7+/16.3+/17)."
  type        = number
  default     = 0
}

variable "aurora_max_acu" {
  type    = number
  default = 4
}

variable "aurora_engine_version" {
  description = "Aurora PostgreSQL 17-compatible engine version."
  type        = string
  default     = "17.5"
}

variable "deletion_protection" {
  description = "Protect Aurora/DynamoDB from deletion. false for a disposable demo (allows terraform destroy)."
  type        = bool
  default     = false
}

# ---------- GitHub OIDC ----------
variable "github_repository" {
  description = "owner/repo allowed to assume the deploy role."
  type        = string
  default     = "zander-zilla/TailwindTraders-Website"
}

variable "github_environment" {
  description = "GitHub Actions environment allowed to assume the deploy role (in addition to the main branch)."
  type        = string
  default     = "aws"
}

variable "create_github_oidc_provider" {
  description = "false if the account already has the token.actions.githubusercontent.com OIDC provider."
  type        = bool
  default     = true
}

# ---------- optional DNS / TLS ----------
variable "domain_name" {
  description = "Optional FQDN for the shop (e.g. shop.example.com). Empty = plain HTTP on the ALB hostname."
  type        = string
  default     = ""
}

variable "route53_zone_id" {
  description = "Optional Route 53 hosted zone id for domain_name (ACM DNS validation). Empty = validate ACM manually."
  type        = string
  default     = ""
}
